using Npgsql;

namespace Orbis.Infrastructure.Persistence;

public enum DatabaseAccessProfile { Service, MembershipAdministration }

public static class DatabasePrivileges
{
    public static async Task<bool> IsSafeAsync(NpgsqlDataSource source, DatabaseAccessProfile profile, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(profile)) throw new ArgumentOutOfRangeException(nameof(profile));
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        // Perfis mutuamente exclusivos impedem trocar as credenciais dos dois hosts.
        await using var command = new NpgsqlCommand("""
            WITH capabilities (relation, can_select, can_insert, can_update, member_columns, tenant_scoped) AS (VALUES
                ('public.memberships', true, false, false, @administration, true),
                ('public.membership_access_changes', @administration, @administration, false, false, true),
                ('public.work_orders', NOT @administration, NOT @administration, NOT @administration, false, true),
                ('public.work_order_audit', NOT @administration, NOT @administration, false, false, true),
                ('public.order_creation_receipts', NOT @administration, NOT @administration, false, false, true),
                ('public.order_transition_receipts', NOT @administration, NOT @administration, false, false, true),
                ('directory.tenants', true, false, false, false, false),
                ('directory.tenant_domains', true, false, false, false, false),
                ('directory.users', true, false, false, false, false),
                ('directory.external_identities', true, false, false, false, false))
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
                        OR (required.can_select AND NOT has_table_privilege(r.oid, c.oid, 'SELECT'))
                        OR (NOT required.can_select AND has_any_column_privilege(r.oid, c.oid, 'SELECT'))
                        OR (required.can_insert AND NOT has_table_privilege(r.oid, c.oid, 'INSERT'))
                        OR (NOT required.can_insert AND has_any_column_privilege(r.oid, c.oid, 'INSERT'))
                        OR (required.can_update AND NOT has_table_privilege(r.oid, c.oid, 'UPDATE'))
                        OR (NOT required.can_update AND NOT required.member_columns AND has_any_column_privilege(r.oid, c.oid, 'UPDATE'))
                        OR (required.member_columns AND (
                            (SELECT count(*) FROM pg_catalog.pg_attribute a WHERE a.attrelid=c.oid AND NOT a.attisdropped
                                AND a.attname IN ('permissions','is_active','version') AND has_column_privilege(r.oid,c.oid,a.attnum,'UPDATE')) <> 3
                            OR EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
                                AND a.attname NOT IN ('permissions','is_active','version') AND has_column_privilege(r.oid,c.oid,a.attnum,'UPDATE'))))
                        OR has_table_privilege(r.oid, c.oid, 'DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
                        OR has_any_column_privilege(r.oid, c.oid, 'REFERENCES')
                        OR has_any_column_privilege(r.oid, c.oid,
                            'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION'))
            FROM pg_catalog.pg_roles r WHERE r.rolname=current_user
            """, connection) { CommandTimeout = 3 };
        command.Parameters.AddWithValue("administration", profile == DatabaseAccessProfile.MembershipAdministration);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
