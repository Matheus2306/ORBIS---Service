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

O script exige PowerShell 7 e PostgreSQL 18, aceita `-PgBin` e `-Port`, cria cluster local exclusivo com SCRAM e credenciais aleatórias, executa todos os testes e encerra o servidor. Não usa bases existentes. Sem PostgreSQL, `dotnet test tests/Orbis.Tests -c Release` executa apenas domínio/arquitetura; isso não substitui integração. Não executar `dotnet test` da solução sem as conexões de teste: a suíte falha intencionalmente quando faltam.

A API expõe detalhe autenticado `GET /v1/work-orders/{id}`, liveness, readiness real de PostgreSQL e OpenAPI em Development. Host verificado, identidade global, membership e permissão de recurso são obrigatórios. Consulte o [contrato/configuração](docs/api/work-orders.md), [evidência de persistência](docs/testing/reports/2026-09-17-tenant-foundation.md) e [evidência HTTP](docs/testing/reports/2026-09-17-authenticated-api.md). Nenhum serviço foi publicado; fornecedor OIDC operacional e portais ainda estão pendentes.
