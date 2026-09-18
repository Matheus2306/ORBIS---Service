using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Infrastructure.Persistence;

namespace Orbis.DataGenerator;

public sealed record TableEvidence(string Table, long Rows, long TotalBytes, string ContentSha256);
public sealed record TenantEvidence(Guid TenantId, long Orders);
public sealed record StatusEvidence(int Status, long Orders);
public sealed record DatasetManifest(string GeneratorVersion, DatasetRecipe Recipe, string PostgreSqlVersion,
    string[] DirectoryMigrations, string[] TenantMigrations, string HashFormat, TableEvidence[] Tables,
    TenantEvidence[] Tenants, StatusEvidence[] Statuses, DateTimeOffset GeneratedAtUtc, double ImportAndVerificationSeconds);

public static class DatasetImporter
{
    public static void ValidateDestination(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        // Guardrail adicional; a operação privilegiada só pertence a um cluster descartável local.
        if (settings.Host != "127.0.0.1" || settings.Database is not { } name ||
            !(name.StartsWith("orbis_perf_", StringComparison.Ordinal) || name.StartsWith("orbis_test_", StringComparison.Ordinal)) ||
            name.Length > 63 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new InvalidOperationException("Dataset generation requires 127.0.0.1 and an isolated orbis_perf_ or orbis_test_ database.");
    }

    public static async Task<DatasetManifest> ImportAsync(string connectionString, DatasetRecipe recipe, CancellationToken cancellationToken = default)
    {
        ValidateDestination(connectionString);
        var watch = Stopwatch.StartNew();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var check = new NpgsqlCommand("SELECT inet_server_addr(), current_database(), rolsuper OR rolbypassrls FROM pg_roles WHERE rolname=current_user", connection))
        await using (var reader = await check.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken) || !IPAddress.IsLoopback(reader.GetFieldValue<IPAddress>(0)) ||
                reader.GetString(1) != connection.Database || !reader.GetBoolean(2))
                throw new InvalidOperationException("The synthetic importer requires a privileged role in the verified local database; never use the API role.");
        }
        // Recusar dados antes das migrations evita até DDL incidental em uma base já utilizada.
        await EnsureEmptyAsync(connection, cancellationToken);
        await using var directory = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory")).Options);
        await using var tenant = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connectionString).Options,
            new TenantScope(recipe.TenantId(0)));
        await directory.Database.MigrateAsync(cancellationToken);
        await tenant.Database.MigrateAsync(cancellationToken);

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        {
            await ExecuteAsync(connection, "SET LOCAL lock_timeout='3s'", cancellationToken);
            await ExecuteAsync(connection, "LOCK TABLE " + string.Join(',', DatasetTables.All.Select(t => t.Name)) + " IN ACCESS EXCLUSIVE MODE", cancellationToken);
            await EnsureEmptyAsync(connection, cancellationToken);
            foreach (var table in DatasetTables.All)
            {
                await using var writer = await connection.BeginBinaryImportAsync($"COPY {table.Name} ({table.Columns}) FROM STDIN (FORMAT BINARY)", cancellationToken);
                writer.Timeout = TimeSpan.FromMinutes(30);
                foreach (var row in table.Rows(recipe))
                {
                    await writer.StartRowAsync(cancellationToken);
                    for (var i = 0; i < row.Length; i++)
                    {
                        if (row[i] is null) await writer.WriteNullAsync(cancellationToken);
                        else await writer.WriteAsync(row[i]!, table.Types[i], cancellationToken);
                    }
                }
                counts.Add(table.Name, checked((long)await writer.CompleteAsync(cancellationToken)));
            }
            await VerifyHistoryAsync(connection, cancellationToken);
        }

        var evidence = new List<TableEvidence>();
        foreach (var table in DatasetTables.All)
        {
            await ExecuteAsync(connection, $"ANALYZE {table.Name}", cancellationToken);
            await using var countCommand = new NpgsqlCommand($"SELECT count(*), pg_total_relation_size('{table.Name}'::regclass) FROM {table.Name}", connection);
            countCommand.CommandTimeout = 1800;
            long rows, size;
            await using (var reader = await countCommand.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                rows = reader.GetInt64(0); size = reader.GetInt64(1);
            }
            if (rows != counts[table.Name]) throw new InvalidOperationException("Dataset row count changed before verification completed.");
            // Hash do conteúdo realmente persistido, em ordem estável; não é hash de estimativa/configuração.
            await using var stream = await connection.BeginRawBinaryCopyAsync($"COPY (SELECT {table.Columns} FROM {table.Name} ORDER BY {table.OrderBy}) TO STDOUT (FORMAT BINARY)", cancellationToken);
            var digest = await SHA256.HashDataAsync(stream, cancellationToken);
            evidence.Add(new(table.Name, rows, size, Convert.ToHexString(digest)));
        }
        var tenants = new List<TenantEvidence>();
        await using (var command = new NpgsqlCommand("SELECT tenant_id,count(*) FROM work_orders GROUP BY tenant_id ORDER BY tenant_id", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) tenants.Add(new(reader.GetGuid(0), reader.GetInt64(1)));
        var statuses = new List<StatusEvidence>();
        await using (var command = new NpgsqlCommand("SELECT status,count(*) FROM work_orders GROUP BY status ORDER BY status", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) statuses.Add(new(reader.GetInt32(0), reader.GetInt64(1)));
        var manifest = new DatasetManifest(DatasetRecipe.GeneratorVersion, recipe, connection.PostgreSqlVersion.ToString(),
            (await directory.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray(),
            (await tenant.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray(), "SHA256/ordered-PostgreSQL-binary-COPY/v1",
            evidence.ToArray(), tenants.ToArray(), statuses.ToArray(), DateTimeOffset.UtcNow, watch.Elapsed.TotalSeconds);
        // O manifesto descreve a mesma versão dos dados; outros escritores aguardam até a verificação terminar.
        await transaction.CommitAsync(cancellationToken);
        return manifest;
    }

    private static async Task EnsureEmptyAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var known = DatasetTables.All.Select(t => t.Name).Concat(["public.__EFMigrationsHistory", "directory.__DirectoryMigrationsHistory"]).ToHashSet(StringComparer.Ordinal);
        var present = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT n.nspname || '.' || c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) present.Add(reader.GetString(0));
        if (present.Any(name => !known.Contains(name))) throw new InvalidOperationException("Unknown tables exist; refusing to touch this database.");
        foreach (var table in DatasetTables.All.Where(t => present.Contains(t.Name, StringComparer.Ordinal)))
        {
            await using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {table.Name})", connection);
            if (await command.ExecuteScalarAsync(cancellationToken) is true) throw new InvalidOperationException("Dataset destination is not empty; existing rows are never removed or replaced.");
        }
    }

    private static async Task VerifyHistoryAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM work_orders o
                LEFT JOIN (SELECT tenant_id,order_id,count(*) n,max(order_version) v FROM work_order_audit GROUP BY tenant_id,order_id) a
                    ON (a.tenant_id,a.order_id)=(o.tenant_id,o.id)
                LEFT JOIN (SELECT tenant_id,order_id,count(*) n,max(version) v FROM order_transition_receipts GROUP BY tenant_id,order_id) t
                    ON (t.tenant_id,t.order_id)=(o.tenant_id,o.id)
                LEFT JOIN (SELECT tenant_id,order_id,count(*) n FROM order_creation_receipts GROUP BY tenant_id,order_id) c
                    ON (c.tenant_id,c.order_id)=(o.tenant_id,o.id)
                WHERE coalesce(a.n,0)<>o.version OR a.v<>o.version OR coalesce(t.n,0)<>o.version-1
                    OR (o.version>1 AND t.v<>o.version) OR coalesce(c.n,0)<>1)
            """, connection) { CommandTimeout = 1800 };
        if (await command.ExecuteScalarAsync(cancellationToken) is true) throw new InvalidOperationException("Synthetic history is inconsistent; the import will roll back.");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 1800 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
