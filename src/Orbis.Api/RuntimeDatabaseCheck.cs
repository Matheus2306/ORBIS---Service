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
        // Conferir capacidades efetivas, inclusive colunas e delegação, antes de confiar na separação administrativa.
        await using var command = new NpgsqlCommand("""
            WITH capabilities (relation, can_insert, can_update, tenant_scoped) AS (VALUES
                ('public.memberships', false, false, true),
                ('public.work_orders', true, true, true),
                ('public.work_order_audit', true, false, true),
                ('public.order_creation_receipts', true, false, true),
                ('public.order_transition_receipts', true, false, true),
                ('directory.tenants', false, false, false),
                ('directory.tenant_domains', false, false, false),
                ('directory.users', false, false, false),
                ('directory.external_identities', false, false, false))
            SELECT current_user = session_user
                AND NOT (r.rolsuper OR r.rolbypassrls OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication)
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles delegated
                    WHERE delegated.oid <> r.oid AND pg_has_role(r.oid, delegated.oid, 'MEMBER'))
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname IN ('public','directory') AND c.relkind IN ('r','p','v','m','f','S')
                        AND pg_has_role(r.oid,c.relowner,'USAGE'))
                AND NOT has_schema_privilege(r.oid, 'public', 'CREATE')
                AND NOT has_schema_privilege(r.oid, 'directory', 'CREATE')
                AND NOT EXISTS (SELECT 1 FROM capabilities required
                    LEFT JOIN pg_catalog.pg_class c ON c.oid = to_regclass(required.relation)
                    WHERE c.oid IS NULL OR c.relkind NOT IN ('r','p')
                        OR (required.tenant_scoped AND NOT (c.relrowsecurity AND c.relforcerowsecurity))
                        OR NOT has_table_privilege(r.oid, c.oid, 'SELECT')
                        OR (required.can_insert AND NOT has_table_privilege(r.oid, c.oid, 'INSERT'))
                        OR (required.can_update AND NOT has_table_privilege(r.oid, c.oid, 'UPDATE'))
                        OR (NOT required.can_insert AND has_any_column_privilege(r.oid, c.oid, 'INSERT'))
                        OR (NOT required.can_update AND has_any_column_privilege(r.oid, c.oid, 'UPDATE'))
                        OR has_table_privilege(r.oid, c.oid, 'DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
                        OR has_any_column_privilege(r.oid, c.oid, 'REFERENCES')
                        OR has_any_column_privilege(r.oid, c.oid,
                            'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION'))
            FROM pg_catalog.pg_roles r WHERE r.rolname=current_user
            """, connection) { CommandTimeout = 3 };
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
