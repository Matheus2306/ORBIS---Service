using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.DataGenerator;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.QueryProbe;

public sealed record SqlParameter(string Name, string Type, object? Value);
public sealed record PlanSample(double PlanningMs, double ExecutionMs, int SharedHitBlocks, int SharedReadBlocks, int TempReadBlocks, int TempWrittenBlocks);
public sealed record QueryEvidence(string Name, string Sql, SqlParameter[] Parameters, JsonElement FirstPlan,
    PlanSample[] Samples, double P50Ms, double P95Ms, double P99Ms, double MaxMs);
public sealed record QueryProbeReport(string Kind, DatasetRecipe Recipe, string PostgreSqlVersion, DateTimeOffset CapturedAtUtc,
    string CurrentRole, bool RowSecurityActive, int DeepOffset, Dictionary<string, string> Settings, QueryEvidence[] Queries);

public static class QueryPlanProbe
{
    public static async Task<QueryProbeReport> CaptureAsync(string runtimeConnection, DatasetRecipe recipe, int repetitions = 30,
        CancellationToken cancellationToken = default)
    {
        DatasetImporter.ValidateDestination(runtimeConnection);
        if (recipe.Level != DatasetLevel.Small || repetitions is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(recipe), "This first probe is bounded to Small and 1..100 serial samples.");
        await using var connection = new NpgsqlConnection(runtimeConnection);
        await connection.OpenAsync(cancellationToken);
        string role;
        await using (var guard = new NpgsqlCommand("""
            SELECT current_user, row_security_active('public.work_orders'), rolsuper OR rolbypassrls,
                has_table_privilege(current_user,'public.memberships','INSERT,UPDATE,DELETE,TRUNCATE')
            FROM pg_roles WHERE rolname=current_user
            """, connection))
        await using (var data = await guard.ExecuteReaderAsync(cancellationToken))
        {
            if (!await data.ReadAsync(cancellationToken) || !data.GetBoolean(1) || data.GetBoolean(2) || data.GetBoolean(3))
                throw new InvalidOperationException("Query evidence requires the restricted runtime role with active RLS.");
            role = data.GetString(0);
        }
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var configure = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='5s'; SELECT set_config('orbis.tenant_id',@tenant,true)", connection))
        {
            configure.Parameters.AddWithValue("tenant", recipe.TenantId(0).ToString());
            await configure.ExecuteNonQueryAsync(cancellationToken);
        }
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("SELECT name,setting FROM pg_settings WHERE name IN ('max_connections','shared_buffers','work_mem','effective_cache_size','random_page_cost','jit','track_io_timing','row_security') ORDER BY name", connection))
        await using (var data = await command.ExecuteReaderAsync(cancellationToken))
            while (await data.ReadAsync(cancellationToken)) settings.Add(data.GetString(0), data.GetString(1));

        var recorder = new CommandRecorder();
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(runtimeConnection).AddInterceptors(recorder).Options;
        await using var directory = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseNpgsql(runtimeConnection).AddInterceptors(recorder).Options);
        var resolved = await new TenantDirectory(directory).ResolveAsync(recipe.Host(0), DatasetRecipe.Issuer,
            DatasetRecipe.Subject(recipe.UserId(0, 0)), cancellationToken);
        if (resolved != new TenantUser(recipe.TenantId(0), recipe.UserId(0, 0)))
            throw new InvalidOperationException("The database does not match the requested synthetic identity recipe.");
        var queries = new List<QueryEvidence>();
        async Task Record(string name, RecordedCommand command) => queries.Add(await MeasureAsync(connection, name, command, repetitions, cancellationToken));
        await Record("directory.lookup", recorder.Last);
        var reader = new WorkOrderReader(options);
        var order = recipe.Orders().First(o => o.ProviderId is not null);
        var actors = new[] { ("dispatcher", recipe.UserId(0, 0)), ("customer", order.CustomerId), ("provider", order.ProviderId!.Value) };
        var dispatcherOffset = 0;
        foreach (var (name, actor) in actors)
        {
            var user = new TenantUser(recipe.TenantId(0), actor);
            var detail = await reader.FindAsync(user, order.Id, cancellationToken);
            if (detail?.Id != order.Id) throw new InvalidOperationException("Authorized detail did not return the expected synthetic order.");
            await Record($"{name}.detail", recorder.Last);
            var firstPage = await reader.ListAsync(user, 25, null, cancellationToken);
            if (firstPage is null || firstPage.Items.Count == 0) throw new InvalidOperationException("Authorized list unexpectedly empty.");
            await Record($"{name}.list.first", recorder.Last);

            await using var database = new TenantDbContext(options, new(user.TenantId));
            await using var scope = await database.BeginTenantTransactionAsync(cancellationToken);
            var member = await database.Memberships.AsNoTracking().SingleAsync(m => m.UserId == actor, cancellationToken);
            await Record($"{name}.membership", recorder.Last);
            var query = database.WorkOrders.AsNoTracking().Where(WorkOrderAccess.ReadFilter(member))
                .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);
            var count = await query.CountAsync(cancellationToken);
            if (name == "dispatcher" && count != recipe.OrdersForTenant(0))
                throw new InvalidOperationException("Dataset volume differs from the recipe; regenerate before measuring.");
            var offset = count * 4 / 5;
            if (name == "dispatcher") dispatcherOffset = offset;
            var previous = await query.Skip(offset - 1).Select(o => new { o.CreatedAt, o.Id }).FirstAsync(cancellationToken);
            // A consulta alternativa existe só no experimento; a API continua usando sua implementação keyset real.
            var offsetRows = await query.Skip(offset).Take(26)
                .Select(o => new { o.Id, o.Description, o.Status, o.Version, o.CreatedAt }).ToListAsync(cancellationToken);
            var offsetSql = recorder.Last;
            var keysetPage = await reader.ListAsync(user, 25, new(previous.CreatedAt, previous.Id), cancellationToken);
            var keysetSql = recorder.Last;
            if (keysetPage is null || !keysetPage.Items.Select(o => o.Id).SequenceEqual(offsetRows.Take(25).Select(o => o.Id)) ||
                keysetPage.HasMore != (offsetRows.Count > 25))
                throw new InvalidOperationException("Keyset/offset do not describe the same authorized page.");
            await Record($"{name}.list.deep.keyset", keysetSql);
            await Record($"{name}.list.deep.offset", offsetSql);
        }
        await transaction.RollbackAsync(cancellationToken);
        return new("serial-instrumented-SQL-plan-probe-not-HTTP-load", recipe, connection.PostgreSqlVersion.ToString(), DateTimeOffset.UtcNow,
            role, true, dispatcherOffset, settings, queries.ToArray());
    }

    private static async Task<QueryEvidence> MeasureAsync(NpgsqlConnection connection, string name, RecordedCommand query,
        int repetitions, CancellationToken cancellationToken)
    {
        // Somente SELECTs produzidos pelos caminhos de leitura; EXPLAIN ANALYZE executa a consulta.
        if (!query.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)) throw new InvalidOperationException("Only reads may be probed.");
        var samples = new List<PlanSample>();
        JsonElement firstPlan = default;
        for (var i = 0; i <= repetitions; i++)
        {
            await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON) " + query.Sql, connection);
            foreach (var parameter in query.Parameters)
                command.Parameters.Add(new NpgsqlParameter(parameter.ParameterName, parameter.NpgsqlDbType) { Value = parameter.Value });
            var json = (string)(await command.ExecuteScalarAsync(cancellationToken))!;
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement[0];
            if (i == 0) continue; // Uma execução de aquecimento declarada, sem misturar cold/warm no relatório.
            if (i == 1) firstPlan = result.Clone();
            var plan = result.GetProperty("Plan");
            int Blocks(string key) => plan.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
            samples.Add(new(result.GetProperty("Planning Time").GetDouble(), result.GetProperty("Execution Time").GetDouble(),
                Blocks("Shared Hit Blocks"), Blocks("Shared Read Blocks"), Blocks("Temp Read Blocks"), Blocks("Temp Written Blocks")));
        }
        var ordered = samples.Select(s => s.ExecutionMs).Order().ToArray();
        double Percentile(double quantile) => ordered[(int)Math.Ceiling(quantile * ordered.Length) - 1];
        return new(name, query.Sql, query.Parameters.Select(p => new SqlParameter(p.ParameterName, p.NpgsqlDbType.ToString(), p.Value)).ToArray(),
            firstPlan, samples.ToArray(), Percentile(.5), Percentile(.95), Percentile(.99), ordered[^1]);
    }

    private sealed record RecordedCommand(string Sql, NpgsqlParameter[] Parameters);

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public RecordedCommand Last { get; private set; } = new(string.Empty, []);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Last = new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(p => new NpgsqlParameter(p.ParameterName, p.NpgsqlDbType) { Value = p.Value }).ToArray());
            return ValueTask.FromResult(result);
        }
    }
}
