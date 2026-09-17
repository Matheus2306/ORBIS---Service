# Threat model — ORBIS SERVICE

## Overview

Modelo inicial em 2026-09-17. Fonte de contexto: requisitos fornecidos pelo usuário e decisões em docs/architecture/adr. Neste snapshot anterior à implementação, não existe endpoint de negócio ou controle de produção verificado. As medidas abaixo são requisitos de design, não findings nem prova de eficácia. Atualizar com referências verificadas ao código após o primeiro incremento.

Fluxo pretendido: browser → ingress confiável → API → resolução do domínio → autenticação → membership/permissão/recurso → transação tenant-scoped → PostgreSQL. IdP é externo e ainda não escolhido. Worker/storage/exports condicionais às features futuras.

| Workflow | Recurso | Configuração/precedência | Local seguro / valor | Atores | Controle | Evidência |
|---|---|---|---|---|---|---|
| Desenvolvimento | Arquivos do projeto | PROJECT_ROOT do ambiente | workspace informado | desenvolvedor/agente | sandbox | escrita local de docs |
| Integração planejada | Cluster PostgreSQL exclusivo | script de teste, porta própria | .artifacts; ignorado pelo Git | test runner | SCRAM + loopback + role restrita | binário 18.6 observado; script ainda pendente |
| API planejada | Dados tenant-scoped | configuração externa de runtime | connection string secreta, valor omitido | API | grants + RLS + filtros + acesso | ADR-002/003; execução pendente |
| Produção planejada | Identidade OIDC | issuer/audience externos | fornecedor não selecionado | IdP/API | validação criptográfica e membership | ADR-004; execução pendente |
| Operação privilegiada | DDL/backup | credencial distinta da API | destino ainda não provisionado | operador autenticado | menor privilégio e trilha operacional | requisito, não implementado |

## Threat Model, Trust Boundaries, and Assumptions

Ativos: dados de clientes, evidências, identidades, permissões, integridade de ordens, disponibilidade, chaves e backups. Atacantes: anônimo controla HTTP/Host/payload; usuário autenticado controla IDs e requisições dentro de sua conta; administrador de tenant controla somente sua organização. Não presumir acesso a credenciais do banco, IdP, operador ou host. Tenants não são autoridades de configuração da plataforma.

Invariantes: isolamento inclusive inferência desnecessária; pertença ativa + permissão + vínculo de recurso; ausência de contexto nega; efeitos sensíveis atômicos/idempotentes; logs sem tokens/PII; privilégio operacional não disponível à API. RLS protege falhas de escopo, não processo comprometido capaz de trocar contexto. DDoS depende também do edge; limiter por processo não oferece quota global. Trust boundaries distintos: browser/API, IdP/API, tenant/control plane, API/DB e operador/deploy.

Suposições ainda não comprovadas: TLS e DNS confiáveis, MFA/revogação no IdP, origem restrita, cofre/rotação, HA, PITR, telemetria protegida e supply chain. Nenhum cache, broker, object storage ou WebSocket existe; ameaças correspondentes são condicionais. Revisão independente do código será feita quando a superfície existir; análise inicial é do autor, sem alegar auditoria independente.

## Attack Surface, Mitigations, and Attacker Stories

| Prioridade | Cenário e ganho | Pré-condição | Impacto | Controle existente | Mitigação exigida | Evidência |
|---|---|---|---|---|---|---|
| Crítica | Tenant A troca ID para ler/escrever B | Endpoint tenant-scoped | Exposição/corrupção cruzada | ainda nenhum endpoint | membership, RLS FORCE, composite FK, testes negativos | ADR-002 |
| Alta | Cliente inicia/conclui ordem de terceiro em A | Conta válida | Fraude/integridade | ainda nenhum endpoint | autorização por recurso além de RBAC | produto/ADR-004 |
| Alta | Host/forwarded header seleciona B | Proxy mal configurado | tenant confusion | configuração não implantada | allowlist proxy, domínio exato, cruzar membership | ADR-002/009 |
| Alta | Token roubado/replay ou issuer errado | Credencial ou validação frouxa | tomada de conta | IdP pendente | assinatura/issuer/audience/expiração, MFA, revogação, cookie seguro | ADR-004 |
| Alta | Contexto A persiste em conexão reutilizada | pooling + estado de sessão | vazamento B→A | persistência pendente | SET LOCAL + transação + teste pool reuse | ADR-003 |
| Alta | Retry ou concorrência duplica conclusão | request repetido | fraude e estado divergente | domínio pendente | versão + chave idempotente transacional | database-design |
| Alta | SQL injection/mass assignment | entrada não parametrizada/DTO amplo | leitura ou escrita indevida | implementação pendente | SQL parametrizado, DTO sem campos de autoridade | security-architecture |
| Alta | Abuso de consultas/uploads/jobs | volume controlado por tenant | indisponibilidade/custo | funções ainda ausentes | limites de custo, paginação, quotas, backpressure | performance/workload |
| Alta | Upload malicioso/path traversal/SSRF | futura feature de anexos/integrações | execução/leitura de rede interna | superfície não implementada | quarentena, chave opaca, allowlist de destino, sem fetch de URL arbitrária | ADR-007 |
| Alta | Segredo em log/build/dependência adulterada | pipeline ou logging inseguro | acesso à plataforma | .gitignore apenas | scans reais, lockfile, versões fixas, review, logs mínimos | test-strategy |
| Média | XSS/CSRF/session fixation | futuro browser flow | ações na sessão | portal não implementado | encoding/CSP, antiforgery, renovação de sessão | ADR-004/010 |
| Média | Enumeração/brute force | endpoint de identidade público | descoberta/abuso | IdP pendente | respostas uniformes, limites, monitoramento | ADR-004 |

Todos os cenários são hipóteses para implementação/revisão, não vulnerabilidades confirmadas.

## Severity Calibration (Critical, High, Medium, Low)

Critical: qualquer vazamento ou mutação cross-tenant efetivo; segredo que permita comprometimento sistêmico. High: privilege escalation dentro de tenant, corrupção de ordens, bypass de autenticação. Medium: enumeração limitada ou degradação demonstrável com pré-condições; não presumir alcance. Low: endurecimento de defesa sem caminho de exploração comprovado. Acesso autorizado do operador não é breakout; ID previsível sem autorização quebrada não basta para um finding. Severidade de uma falha exige evidência do caminho e do ganho de autoridade.
