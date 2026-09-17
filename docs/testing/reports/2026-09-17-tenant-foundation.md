# Evidência inicial de isolamento e concorrência

Data: 2026-09-17. Revisão: incremento de persistência que introduz este relatório, sobre base `aff8d73`. Ambiente: Windows x64, .NET SDK 10.0.302, runtime 10.0.10, EF 10.0.12/Npgsql EF 10.0.3, PostgreSQL 18.6 local, 8 processadores lógicos reportados pelo processo. Não é ambiente equivalente à produção.

Execução: `dotnet build -c Release --no-restore -warnaserror`, seguido de `./scripts/test-postgres.ps1`. Cluster exclusivo, loopback, SCRAM, max_connections=40, shared_buffers=64MB; pool runtime=12, admin=5. Credenciais efêmeras não incluídas. Sem mocks/SQLite nos testes de persistência.

Resultado: build sem erros/avisos; **13 unitários/arquitetura + 11 integração/contrato = 24 aprovados, zero falhas e zero ignorados**. Integração levou aproximadamente 6s; essa duração não é latência de endpoint nem benchmark de capacidade.

Controles exercitados: leitura própria; leitura de B sob contexto A retorna ausência mesmo com IgnoreQueryFilters; INSERT cross-tenant retorna insufficient_privilege; UPDATE de ID alheio afeta zero linhas; FK composta rejeita cliente de outro tenant; SaveChanges rejeita entidade fora de escopo e transação ausente; role runtime sem ownership/superuser/BYPASSRLS/create role/create DB; TRUNCATE negado; RLS ENABLE/FORCE nas duas tabelas; mesma conexão física reutilizada sem contexto após commit e rollback. Cem cópias carregadas da versão original disputam cancelamento: exatamente um commit, estado Cancelled e versão 2. Não são cem usuários HTTP nem cem conexões de banco simultâneas.

Contrato HTTP atual: live 200, ready 503 e OpenAPI ausente em Testing. Endpoints de negócio ainda ausentes, portanto IDOR via HTTP, JWT/host/membership e idempotência não foram validados.

Artefatos locais ignorados pelo Git: `.artifacts/postgres/e6a5090a40f948f5ad1099eea4f825b7/test-results/`. SHA-256 dos TRX: unitários `C8FBA80C668527134B4619903BEBFAC16DD03DCE16C58108E316CB80674B459E`; integração `953D5BD9C4A7E40EC692DC5C2629508A741DA2A6FD70075ECBE2BFBE86200A49`. Cluster encerrado pelo script. O primeiro ensaio não chegou aos testes por espera de processo descendente; corrigido com espera limitada do pg_ctl e registrado sem aceitar resultado inexistente.

Segurança preliminar: feed NuGet consultado com dependências transitivas, sem advisories reportados; Gitleaks 8.30.1 baixado de release oficial, checksum verificado, working tree escaneada com redaction, sem leaks. Não equivale a auditoria completa, DAST ou scan de imagem. Revisão independente indisponível; arquitetura revisada sequencialmente e lacuna registrada no threat model.

**VALIDADO:** comportamento dos casos acima em pequeno fixture. **PROJETADO:** workload de 1M em docs/performance. **NÃO VALIDADO:** RPS, p95/p99, dataset Small completo, hot tenant, noisy neighbor, migração em volume, failover, restore e escala de 1M.
