# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-22
- Fase: expansão funcional da API; auditoria/catálogo e controllers concluídos; baseline funcional pendente
- Branch: main
- Último commit anterior ao incremento atual: e99b874; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 153 aprovados, 0 falhas/ignorados (25 domínio/arquitetura/cursor + 128 integração); 26 casos das regras e 12 checks do gate
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: contexto atual de identidade/tenant/permissões implementado e validado; relatório 2026-09-22-current-context
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: consultar memberships com permissão específica e paginação, depois administração/provisionamento
- Próximas cinco ações: leitura de membros; administração/provisionamento/IdP; clientes; prestadores; catálogo/solicitações; F5/F6 continuam gates
- Domínio atual: Membership, próximo incremento P0
- Último domínio concluído: nenhum baseline de domínio completo; núcleo WorkOrders testado, controllers concluídos
- Endpoints catalogados: 111; implementados: 14 (11 negócio + 3 técnicos); integration-tested: 14; security-tested de negócio: 11; performance HTTP: 0
- P0 restantes: 43 operações; P1 restantes: 42, conforme endpoint-catalog; gates operacionais adicionais pendentes
- Próximos endpoints: GET /v1/members e GET /v1/members/{id}; ainda não implementados

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
