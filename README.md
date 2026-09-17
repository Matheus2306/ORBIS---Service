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

Comandos de build, testes e operação são adicionados com seus respectivos executáveis; consulte [validação](docs/testing/test-strategy.md). Nenhum serviço foi publicado.
