# Modelo de dados e operação

## Fronteiras e entidades

Control plane implementado no schema directory: Tenant(id,name,is_active), TenantDomain(host,tenant_id,is_verified), User(id,is_active), ExternalIdentity(issuer,subject,user_id). Directory lookup não permite listar dados dos tenants. Host único normalizado IDN/ASCII, verificação explícita por operação privilegiada; prova de posse automatizada e onboarding pendentes. Tenants/domínios começam inativos/não verificados. Issuer+subject usam collation C, sem normalização que una identidades distintas. Tabelas globais são SELECT-only para runtime; não possuem RLS de tenant.

Tenant plane: Membership(tenant_id,user_id,status,permissions), Unit(tenant_id,id), Service(tenant_id,id), CustomerProfile(tenant_id,user_id), ProviderProfile(tenant_id,user_id), WorkOrder(tenant_id,id,customer_user_id,provider_user_id,status,version,created_at), Audit(tenant_id,id,actor_id,action,resource_id,at). Catálogo/unidades/perfis/auditoria são planejados; implementar incrementos e migrations correspondentes, sem alegar existência da tabela por estar neste desenho.

PK (tenant_id,id); membership PK (tenant_id,user_id). Toda referência tenant-scoped inclui tenant_id; FK de executor/cliente para membership impede associação cruzada. UUID v7 para novas entidades; dados sintéticos usam IDs determinísticos. Dados pessoais mínimos; email pertence à identidade, não chave de relação. Texto com limites e moeda explícita quando preços forem implementados.

Índices propostos: ordens (tenant_id,created_at,id) para keyset; (tenant_id,customer_user_id,created_at,id) e (tenant_id,provider_user_id,created_at,id) conforme planos medidos. Não criar cada combinação antecipadamente. Evitar count global por página; pedir `limit+1` e cursor validado. Offset só para pequenas listas e com comparação medida. Busca textual sem wildcard ilimitado.

## Integridade e isolamento

Constraints de enum/ranges/not-null e FKs além da validação de domínio. Version bigint para concorrência otimista; update condicional retorna conflito, não overwrite. Transação Read Committed para agregados; invariantes que atravessam linhas usam constraint ou lock ordenado específico, não serializable global. Idempotência: chave única (tenant,actor,operation,key), fingerprint do payload, resposta limitada, TTL e atomicidade com efeito. Mesma chave e payload diferente retorna conflito; retries paralelos convergem.

RLS com USING e WITH CHECK, ENABLE/FORCE, runtime sem bypass. Tenant em SET LOCAL via set_config dentro da mesma transação/conexão da consulta. Contexto nunca herdado de request anterior; filtros EF são segunda linha. SaveChanges rejeita mudança de tenant e entidade fora do escopo. SQL bruto é restrito e testado. Runtime não aplica migrations, não tem DDL/TRUNCATE. Erros de constraints não revelam existência de IDs globais.

## Crescimento e disponibilidade

Pool inicial pequeno e parametrizado, timeout de aquisição/conexão/comando e cancellation propagado; orçamento total documentado por implantação. Reserva administrativa necessária. Avaliar pooler apenas com evidência; transaction pooling exige revisar estado de sessão/prepared statements. Sem N+1 ou Includes de grafos para lista.

Capturar pg_stat_activity, pg_locks, deadlocks, query plans e índices; transações curtas, ordem estável de locks. Autovacuum não desabilitado; acompanhar dead tuples, age de xid, bloat, WAL e lag. Logs de queries não contêm parâmetros sensíveis. pg_stat_statements requer configuração explícita no ambiente; não presumir que está habilitado.

Sem particionamento inicial. Testar em histórico de 100M antes de selecionar por tempo para audit/history; índices e unicidade global exigem revisão. Read replicas só para leitura tolerante a atraso, jamais read-after-write/autorização sem política de staleness. Sharding futuro por tenant com diretório de placement; proibidos joins de domínio entre shards.

HA, backups base + WAL/PITR, criptografia e cópia independente são requisitos operacionais pendentes. Restore por tenant em shared DB requer instância auxiliar e exportação consistente, não simples restore do cluster vivo. Migration: expand/backfill em lotes/contract, medir locks em Medium+, lock_timeout e statement_timeout, índices concurrently quando aplicável, N/N+1 e interrupção testados. Reverter código não implica executar Down destrutivo.

## Ordem operacional de migrations

Há dois contextos e históricos: DirectoryDbContext usa directory.__DirectoryMigrationsHistory; TenantDbContext usa o histórico padrão. Em instalação nova, aplicar diretório antes da sequência tenant; runtime não executa migrations. Em upgrade do commit fdb089c, criar diretório e então LinkDirectory. Essa migration preserva ordens e cria entradas de legado INATIVAS para tenants/usuários já existentes, sem inventar domínios/identidades. Exige role operacional que possa enxergar todas as linhas (`row_security=off` evita backfill parcial silencioso), lock_timeout 3s e FKs membership→diretório. As FKs entre contextos e índice memberships(user_id) são administrados pelo SQL de LinkDirectory, deliberadamente fora do modelo EF tenant.

Testes reais partem do schema tenant inicial populado, aplicam diretório/link e verificam preservação e negação de acesso implícito. Não há medida de duração/locks em Medium+ nem garantia N/N+1 para o antigo código de provisionamento. Não aplicar esse backfill monolítico em tabelas grandes sem substituição por expand/backfill em lotes/validate. Down do link remove somente FKs/índice; não apaga entradas globais preservadas. Um rollback de diretório exige desfazer dependências primeiro e análise de dados; não executar automaticamente.
