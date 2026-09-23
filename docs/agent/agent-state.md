# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-23
- Fase: expansão funcional da API; auditoria/catálogo e controllers concluídos; baseline funcional pendente
- Branch: main
- Último commit anterior ao incremento atual: 8cb2a78; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 213 aprovados, 0 falhas/ignorados (31 domínio/arquitetura/cursor + 182 integração); 26 casos das regras e 12 checks do gate
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: núcleo transacional de administração de memberships concluído internamente; gate completo213/213 aprovado; relatório2026-09-23-membership-administration-core e ADR-016
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: implementar host/credencial administrativos conforme ADR-015, sem ampliar grants ou memberships da role comum
- Próximas cinco ações: fronteira administrativa; convites/IdP; clientes; prestadores; catálogo/solicitações; F5/F6 continuam gates
- Domínio atual: Membership; consulta com version e núcleo de comandos implementados; exposição HTTP administrativa pendente
- Último domínio concluído: nenhum baseline de domínio completo; núcleo WorkOrders testado, controllers concluídos
- Endpoints catalogados: 111; implementados: 16 (13 negócio + 3 técnicos); integration-tested: 16; security-tested de negócio: 13; performance HTTP: 0
- P0 restantes: 41 operações; P1 restantes: 42, conforme endpoint-catalog; gates operacionais adicionais pendentes
- Próximos endpoints: convites e gestão delegada de memberships; implementar boundary privilegiada antes dos comandos HTTP

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
