-- Executar após migrations com o proprietário; a role deve existir sem memberships nem privilégios globais.
-- Credencial exclusiva do futuro host administrativo. Não utilizar orbis_runtime nem a credencial de migrations.
REVOKE ALL ON SCHEMA public, directory FROM PUBLIC;
GRANT USAGE ON SCHEMA public, directory TO orbis_membership_admin;
GRANT SELECT ON memberships TO orbis_membership_admin;
-- IDs não podem ser reatribuídos e este fluxo não cria/remove memberships.
GRANT UPDATE (permissions, is_active, version) ON memberships TO orbis_membership_admin;
GRANT SELECT, INSERT ON membership_access_changes TO orbis_membership_admin;
GRANT SELECT ON directory.tenants, directory.tenant_domains, directory.users, directory.external_identities TO orbis_membership_admin;
