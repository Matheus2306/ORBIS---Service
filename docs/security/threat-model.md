# Threat model — ORBIS SERVICE

## Overview

Modelo atualizado em 2026-09-17 após a fundação de persistência. Fontes: código inspecionado, testes locais reais e requisitos do usuário. Existe domínio e isolamento de banco, mas a API ainda expõe somente health/OpenAPI em Development; não há endpoint de negócio, autenticação OIDC ou configuração de produção. Controles implementados e arquitetura planejada estão separados abaixo. Cenários de ameaça não são findings.

Fluxo pretendido: browser → ingress confiável → API → resolução do domínio → autenticação → membership/permissão/recurso → transação tenant-scoped → PostgreSQL. IdP é externo e ainda não escolhido. Worker/storage/exports condicionais às features futuras.

| Workflow | Recurso | Configuração/precedência | Local seguro / valor | Atores | Controle | Evidência |
|---|---|---|---|---|---|---|
| Desenvolvimento | Arquivos do projeto | PROJECT_ROOT do ambiente | workspace informado | desenvolvedor/agente | sandbox | escrita local de docs |
| Integração local | Cluster PostgreSQL exclusivo | PgBin/Port do script; conexões efêmeras via ambiente | .artifacts/postgres/{UUID}/data; loopback:55439 por padrão | runner privilegiado e role runtime distinta | SCRAM; nomes únicos; shutdown no finally | scripts/test-postgres.ps1:7,27,30,44,60 |
| Persistência nos testes | Dados tenant-scoped | ORBIS_TEST_RUNTIME_CONNECTION validada pelo fixture | banco local orbis_test_{UUID}; pool máximo 12 | orbis_runtime, sem owner/bypass/DDL | grants, RLS, filtros e checks de escrita | tests/Orbis.IntegrationTests/DatabaseFixture.cs:28,55; TenantDbContext abaixo |
| API atual | health e contrato OpenAPI | ASPNETCORE_ENVIRONMENT; defaults de ASP.NET | sem conexão de banco registrada; readiness 503 | cliente HTTP | ausência de rotas de negócio; limite body 32 KiB | src/Orbis.Api/Program.cs:11,16,18,20 |
| Scaffolding de migration | Modelo EF | DesignTimeContextFactory, sem segredo | 127.0.0.1/orbis_design_only, timeout 2s; conexão operacional deve ser explícita | operador local | execução separada da API | src/Orbis.Infrastructure/Persistence/DesignTimeContextFactory.cs:13 |
| Produção planejada | Identidade OIDC | issuer/audience externos | fornecedor não selecionado | IdP/API | validação criptográfica e membership | ADR-004; execução pendente |
| Operação privilegiada | DDL/backup | credencial distinta da API | destino ainda não provisionado | operador autenticado | menor privilégio e trilha operacional | requisito, não implementado |

## Threat Model, Trust Boundaries, and Assumptions

Ativos: dados de clientes, evidências, identidades, permissões, integridade de ordens, disponibilidade, chaves e backups. Atacantes: anônimo controla HTTP/Host/payload; usuário autenticado controla IDs e requisições dentro de sua conta; administrador de tenant controla somente sua organização. Não presumir acesso a credenciais do banco, IdP, operador ou host. Tenants não são autoridades de configuração da plataforma.

Invariantes: isolamento inclusive inferência desnecessária; pertença ativa + permissão + vínculo de recurso; ausência de contexto nega; efeitos sensíveis atômicos/idempotentes; logs sem tokens/PII; privilégio operacional não disponível à API. RLS protege falhas de escopo, não processo comprometido capaz de trocar contexto. DDoS depende também do edge; limiter por processo não oferece quota global. Trust boundaries distintos: browser/API, IdP/API, tenant/control plane, API/DB e operador/deploy.

Suposições ainda não comprovadas: TLS e DNS confiáveis, MFA/revogação no IdP, origem restrita, cofre/rotação, HA, PITR, telemetria protegida e supply chain. Nenhum cache, broker, object storage ou WebSocket existe; ameaças correspondentes são condicionais. A tentativa de revisão independente não executou por indisponibilidade de créditos do worker; revisão de arquitetura feita sequencialmente pelo autor, conforme fallback da skill. Não equivale a auditoria independente.

Controles reais: filtros em `src/Orbis.Infrastructure/Persistence/TenantDbContext.cs:29,48`; FKs compostas em `:50,51`; transação e set_config local em `:55,61`; write guard em `:83`; versão de concorrência em `:46`. RLS ENABLE/FORCE/USING/WITH CHECK na migration `src/Orbis.Infrastructure/Persistence/Migrations/20260917130050_InitialTenantBoundary.cs:84`. Acesso por vínculo/recurso em `src/Orbis.Application/WorkOrders/WorkOrderAccess.cs:8,18,28`; suspensão em `src/Orbis.Domain/Identity/Membership.cs:29,33`. Esses helpers ainda não estão ligados a endpoints de negócio.

Questões resolvidas: o runtime restrito dos testes não é owner/superuser/BYPASSRLS; ignorar filtro EF não revela B; conexão reutilizada perde contexto após commit/rollback; 100 escritores da versão original geram um único efeito. Evidência: `tests/Orbis.IntegrationTests/TenantDatabaseTests.cs:10,71,93,126` e relatório de integração. Questões abertas: grants de produção só existem no fixture local; não há registry/host resolver/identidade global persistida ainda; nenhuma conexão de API pode ser considerada protegida antes dessa integração. Não expor o DbContext diretamente como endpoint.

## Attack Surface, Mitigations, and Attacker Stories

| Prioridade | Cenário e ganho | Pré-condição | Impacto | Controle existente | Mitigação exigida | Evidência |
|---|---|---|---|---|---|---|
| Crítica | Tenant A troca ID para ler/escrever B | Futuro endpoint tenant-scoped | Exposição/corrupção cruzada | RLS/filtros/FK testados no banco; HTTP de negócio ausente | integrar identidade/membership/recurso e testar via HTTP | TenantDbContext:29,48,50; migration:84 |
| Alta | Cliente inicia/conclui ordem de terceiro em A | Conta válida | Fraude/integridade | ainda nenhum endpoint | autorização por recurso além de RBAC | produto/ADR-004 |
| Alta | Host/forwarded header seleciona B | Proxy mal configurado | tenant confusion | configuração não implantada | allowlist proxy, domínio exato, cruzar membership | ADR-002/009 |
| Alta | Token roubado/replay ou issuer errado | Credencial ou validação frouxa | tomada de conta | IdP pendente | assinatura/issuer/audience/expiração, MFA, revogação, cookie seguro | ADR-004 |
| Alta | Contexto A persiste em conexão reutilizada | pooling + estado de sessão | vazamento B→A | SET LOCAL e pool reuse real testados após commit/rollback | manter teste obrigatório em cada mudança de conexão/pooler | TenantDbContext:61; TenantDatabaseTests:93 |
| Alta | Retry ou concorrência duplica conclusão | futuro comando HTTP repetido | fraude e estado divergente | version token testado com 100 escritores; idempotência ainda ausente | chave idempotente transacional e audit antes da exposição | TenantDbContext:46; TenantDatabaseTests:126 |
| Alta | SQL injection/mass assignment | entrada não parametrizada/DTO amplo | leitura ou escrita indevida | set_config parametrizado, sem DTO HTTP de negócio | DTO sem campos de autoridade; manter SQL parametrizado | TenantDbContext:61 |
| Alta | Abuso de consultas/uploads/jobs | volume controlado por tenant | indisponibilidade/custo | funções ainda ausentes | limites de custo, paginação, quotas, backpressure | performance/workload |
| Alta | Upload malicioso/path traversal/SSRF | futura feature de anexos/integrações | execução/leitura de rede interna | superfície não implementada | quarentena, chave opaca, allowlist de destino, sem fetch de URL arbitrária | ADR-007 |
| Alta | Segredo em log/build/dependência adulterada | pipeline ou logging inseguro | acesso à plataforma | .gitignore apenas | scans reais, lockfile, versões fixas, review, logs mínimos | test-strategy |
| Média | XSS/CSRF/session fixation | futuro browser flow | ações na sessão | portal não implementado | encoding/CSP, antiforgery, renovação de sessão | ADR-004/010 |
| Média | Enumeração/brute force | endpoint de identidade público | descoberta/abuso | IdP pendente | respostas uniformes, limites, monitoramento | ADR-004 |

Todos os cenários são hipóteses para implementação/revisão, não vulnerabilidades confirmadas.

## Severity Calibration (Critical, High, Medium, Low)

Critical: qualquer vazamento ou mutação cross-tenant efetivo; segredo que permita comprometimento sistêmico. High: privilege escalation dentro de tenant, corrupção de ordens, bypass de autenticação. Medium: enumeração limitada ou degradação demonstrável com pré-condições; não presumir alcance. Low: endurecimento de defesa sem caminho de exploração comprovado. Acesso autorizado do operador não é breakout; ID previsível sem autorização quebrada não basta para um finding. Severidade de uma falha exige evidência do caminho e do ganho de autoridade.
