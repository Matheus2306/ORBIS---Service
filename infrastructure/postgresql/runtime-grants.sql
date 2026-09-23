-- Executar com o proprietário após migrations; o runtime nunca pode conceder privilégios a si mesmo.
-- Role de propósito único, sem memberships ou GRANT OPTION. Não compartilhar credencial com administração (ADR-015).
-- Este bootstrap não revoga grants extras pré-existentes por tabela/coluna/role: investigar drift rejeitado pelo guard.
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO orbis_runtime;
GRANT SELECT ON memberships TO orbis_runtime;
GRANT SELECT, INSERT, UPDATE ON work_orders TO orbis_runtime;
GRANT SELECT, INSERT ON work_order_audit, order_creation_receipts, order_transition_receipts TO orbis_runtime;
REVOKE ALL ON SCHEMA directory FROM PUBLIC;
GRANT USAGE ON SCHEMA directory TO orbis_runtime;
GRANT SELECT ON directory.tenants, directory.tenant_domains, directory.users, directory.external_identities TO orbis_runtime;
