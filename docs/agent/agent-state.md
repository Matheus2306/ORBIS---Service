# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-18
- Fase: 0 concluída; F5 em curso, dataset Small reproduzido
- Branch: main
- Último commit anterior ao incremento atual: a3b032e; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 114 aprovados, 0 falhas/ignorados (25 domínio/arquitetura/cursor + 89 PostgreSQL/HTTP/dataset/TLS)
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: PostgreSQL local exige TLS/VerifyFull; próximo harness HTTP para Performance
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: harness Kestrel real/HTTPS e carga HTTP autenticada/instrumentada
- Próximas cinco ações: preparar harness HTTP; baseline de carga; CI reproduzível; quotas/retenção; ampliar dataset após medir recursos

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
