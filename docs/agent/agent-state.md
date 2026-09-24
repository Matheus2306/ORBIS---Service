# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-24
- Fase: expansão funcional da API; auditoria/catálogo e controllers concluídos; baseline funcional pendente
- Branch: main
- Último commit anterior ao incremento atual: 6ee4453; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 262 aprovados, 0 falhas/ignorados (33 domínio/arquitetura/cursor + 229 integração); 26 casos das regras e 12 checks do gate
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: host e três controllers administrativos concluídos; gate262/262 aprovado, publish local dos dois hosts aprovado; relatório2026-09-24-administrative-http e ADR-017
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: definir e implementar convites expiráveis/de uso único vinculados à identidade destinatária verificada; preservar fronteira administrativa e não ampliar credencial comum
- Próximas cinco ações: convites/aceite; IdP e bootstrap operacional; clientes; prestadores; catálogo/solicitações; F5/F6 continuam gates
- Domínio atual: Membership; consulta com version e comandos HTTP de permissões/suspensão/reativação em host separado; convites/provisionamento pendentes
- Último domínio concluído: nenhum baseline de domínio completo; núcleo WorkOrders testado, controllers concluídos
- Endpoints catalogados: 111; implementados: 19 (16 negócio + 3 técnicos únicos); integration-tested: 19; security-tested de negócio: 16; performance HTTP: 0
- P0 restantes: 38 operações; P1 restantes: 42, conforme endpoint-catalog; gates operacionais adicionais pendentes
- Próximos endpoints: convites/revogação/aceite; identidade de operador global explícita antes de provisionar tenants

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
