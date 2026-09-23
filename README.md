# ORBIS SERVICE

SaaS B2B2C multi-tenant para contratação e execução de serviços. .NET 10 / ASP.NET Core 10 / EF Core 10 / PostgreSQL 18.

**Estado: engenharia inicial; produção bloqueada. Capacidade de 1 milhão NÃO VALIDADA.** Arquitetura e metas não são evidência de capacidade.

Comece por [estado do agente](docs/agent/agent-state.md), [próximas ações](docs/agent/next-actions.md), [produto](docs/product/product-definition.md), [backlog](docs/product/backlog.md), [decisões](docs/architecture/adr/ADR-001-application-architecture.md) e [modelo de carga](docs/performance/workload-model.md).

## Regras do produto

- Tenant é fronteira de segurança. Identidade global e memberships independentes por organização.
- Negação por padrão. Tenant recebido do cliente não autoriza acesso.
- Capacidade publicada somente com relatórios reproduzíveis e limites explícitos.
- Configuração externa; nenhum segredo versionado; migrations fora do startup da API.
- Cada incremento deve manter domínio, autorização, persistência e testes coerentes.

## Ambiente

SDK fixado em `global.json`. Development, Testing, Performance, Staging e Production são ambientes distintos. Não reutilizar dados pessoais ou credenciais entre eles. A pasta `.artifacts` contém somente artefatos locais ignorados pelo Git.

```powershell
dotnet restore --locked-mode
dotnet tool restore
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore -warnaserror
./scripts/test-postgres.ps1
```

Antes da primeira suíte completa, prepare o [gerador HTTPS fixado](performance-tests/vegeta/README.md) com `./scripts/build-load-generator.ps1`. O script de testes exige PowerShell 7 e PostgreSQL 18, aceita `-PgBin` e `-Port`, cria cluster local exclusivo com SCRAM, credenciais aleatórias e [TLS com CA efêmera verificada](docs/testing/local-postgres-tls.md), executa todos os testes e encerra o servidor. Não usa bases existentes. Sem PostgreSQL, `dotnet test tests/Orbis.Tests -c Release` executa apenas domínio/arquitetura; isso não substitui integração. Não executar `dotnet test` da solução sem as conexões de teste: a suíte falha intencionalmente quando faltam.

Para reunir restore/build/testes/scanners/migrations e registrar evidências, use `./scripts/validate-local.ps1` conforme o [gate local](docs/testing/local-validation.md). A execução [aprovada](docs/testing/reports/2026-09-21-local-gate.md) valida engenharia local; CI remoto e gates de produção continuam pendentes.

A API expõe detalhe/lista autenticados, criação idempotente e atribuição/aceite/início/conclusão/cancelamento com versão e audit transacional. Possui liveness, readiness PostgreSQL e OpenAPI em Development. Host verificado, identidade global, membership e permissão de recurso são obrigatórios. Consulte o [contrato/configuração](docs/api/work-orders.md) e [evidência de 118 testes, incluindo HTTPS real](docs/testing/reports/2026-09-21-kestrel-https.md). Nenhum serviço foi publicado; fornecedor OIDC operacional, portais, performance e operação ainda estão pendentes.

Também estão implementados o [contexto atual](docs/api/current-context.md) e a [consulta de membros](docs/api/membership-read.md), com permissão ReadMembers, paginação e filtros. O [catálogo](docs/api/endpoint-catalog.md) distingue as 16 operações existentes das capacidades ainda planejadas.

O [núcleo administrativo de memberships](docs/api/membership-administration.md) valida delegação, versão, último administrador e audit/recibo atômicos em PostgreSQL. Consultas incluem `version`; endpoints de escrita dependem do host administrativo separado e MFA. [Gate atual:213 testes/12 checks](docs/testing/reports/2026-09-23-membership-administration-core.md), sem validação de capacidade ou produção.

As rotas de negócio estão organizadas em controllers de ordens e transições, com contratos em `src/Orbis.Api/Contracts`. Health checks e OpenAPI mantêm os componentes nativos. A expansão está registrada na [auditoria de lacunas](docs/api/api-gap-analysis.md), [matriz de 31 domínios](docs/api/domain-capability-matrix.md), [catálogo de operações](docs/api/endpoint-catalog.md) e [matriz de autorização](docs/security/authorization-matrix.md). Rotas planejadas não estão disponíveis no servidor.

O [gerador sintético](docs/database/dataset-plan.md) prepara Small com 1.000 usuários, 50 tenants e 10.000 ordens, perfis uniforme/concentrado e manifesto de hashes/contagens reais. Execute `./scripts/generate-dataset.ps1` após build Release. Número de registros não é capacidade de usuários simultâneos; nenhum RPS/p95/p99 da API foi validado.

Os [planos SQL Small](docs/performance/reports/2026-09-18-small-query-plans.md) foram capturados com runtime restrito e RLS, incluindo páginas keyset/offset equivalentes. Reprodução: `./scripts/capture-query-plans.ps1 -Profile HotTenant`. São medições seriais de consultas, não teste de carga da API.

A [validação do contexto atual](docs/testing/reports/2026-09-22-current-context.md) aprovou 153 testes, 26 casos das regras e 12 checks do gate local. Inclui contratos dos controllers, identidade/tenant/permissões, atributos controlados de métricas HTTP/TLS/pool e gerador HTTPS externo auditado. O [estado do agente](docs/agent/agent-state.md) identifica a evidência mais recente. [Observabilidade](docs/operations/observability.md) distingue emissão de sinais de coleta/alertas pendentes. Esses testes não comprovam carga ou escala.
