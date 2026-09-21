# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-21
- Fase: 0 concluída; F5 e F6 em curso, dataset Small e gate local validados
- Branch: main
- Último commit anterior ao incremento atual: 8938aa8; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 119 aprovados, 0 falhas/ignorados (25 domínio/arquitetura/cursor + 94 PostgreSQL/HTTP/dataset/TLS/métricas)
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: gate local aprovado com 119 testes e 21 regras de evidência; gerador/coletor de carga pendentes
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: escolher e experimentar gerador de carga com validação HTTPS e modelo de chegada explícito
- Próximas cinco ações: gerador de carga HTTPS; host Performance separado; telemetria de recursos; baseline de carga; CI remoto

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
