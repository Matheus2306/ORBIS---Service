using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Orbis.Api;

public sealed class RuntimeDatabaseCheck(NpgsqlDataSource dataSource, TimeProvider timeProvider) : IHealthCheck, IHostedService, IDisposable
{
    private readonly SemaphoreSlim probeLock = new(1, 1);
    private ProbeResult? latest;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await IsSafeAsync(cancellationToken))
            throw new InvalidOperationException("Runtime database privileges or tenant protections are unsafe.");
        Volatile.Write(ref latest, new ProbeResult(timeProvider.GetTimestamp(), HealthCheckResult.Healthy()));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cached = Volatile.Read(ref latest);
        if (IsFresh(cached)) return cached!.Result;

        // Somente a saúde é reutilizada; identidade e permissões continuam consultadas em cada acesso.
        await probeLock.WaitAsync(cancellationToken);
        try
        {
            cached = Volatile.Read(ref latest);
            if (IsFresh(cached)) return cached!.Result;
            HealthCheckResult result;
            try
            {
                result = await IsSafeAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                // Falhas também expiram: uma indisponibilidade não deve causar uma tempestade de probes.
                result = HealthCheckResult.Unhealthy();
            }
            Volatile.Write(ref latest, new ProbeResult(timeProvider.GetTimestamp(), result));
            return result;
        }
        finally { probeLock.Release(); }
    }

    private bool IsFresh(ProbeResult? probe) => probe is not null &&
        timeProvider.GetElapsedTime(probe.Timestamp) < TimeSpan.FromSeconds(5);

    public void Dispose() => probeLock.Dispose();

    private sealed record ProbeResult(long Timestamp, HealthCheckResult Result);

    private async Task<bool> IsSafeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // Uma connection string privilegiada não pode transformar RLS em proteção apenas aparente.
        await using var command = new NpgsqlCommand("""
            SELECT NOT (r.rolsuper OR r.rolbypassrls OR r.rolcreatedb OR r.rolcreaterole)
                AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname IN ('public','directory') AND c.relkind='r' AND pg_has_role(r.oid,c.relowner,'USAGE'))
                AND (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='public' AND c.relname IN ('memberships','work_orders')
                        AND c.relrowsecurity AND c.relforcerowsecurity)=2
                AND NOT has_schema_privilege(current_user, 'public', 'CREATE')
                AND NOT has_schema_privilege(current_user, 'directory', 'CREATE')
                AND NOT has_table_privilege(current_user, 'work_orders', 'TRUNCATE')
                AND NOT has_table_privilege(current_user, 'memberships', 'INSERT,UPDATE,DELETE,TRUNCATE')
                AND NOT has_table_privilege(current_user, 'directory.tenants', 'INSERT,UPDATE,DELETE,TRUNCATE')
                AND NOT has_table_privilege(current_user, 'directory.tenant_domains', 'INSERT,UPDATE,DELETE,TRUNCATE')
                AND NOT has_table_privilege(current_user, 'directory.users', 'INSERT,UPDATE,DELETE,TRUNCATE')
                AND NOT has_table_privilege(current_user, 'directory.external_identities', 'INSERT,UPDATE,DELETE,TRUNCATE')
            FROM pg_roles r WHERE r.rolname=current_user
            """, connection) { CommandTimeout = 3 };
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
