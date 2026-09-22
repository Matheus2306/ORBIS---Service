# Estado do agente

- Projeto: ORBIS SERVICE
- PROJECT_ROOT: C:\Users\Usuario\ORBIS - Service
- Data: 2026-09-22
- Fase: 0 concluída; F5 e F6 em curso, dataset Small, gerador HTTPS e gate local validados
- Branch: main
- Último commit anterior ao incremento atual: e4192ff; revisão corrente em `git log -1`
- Build: Release aprovado, 0 warnings/0 errors
- Testes: 126 aprovados, 0 falhas/ignorados (25 domínio/arquitetura/cursor + 101 integração); 26 casos das regras do gate
- Performance baseline HTTP: inexistente; planos SQL Small instrumentados em reports/2026-09-18-small-query-plans.md
- Maior carga validada: nenhuma
- RPS validado / p95 / p99 / error rate: não medidos
- Arquitetura: monólito modular; PostgreSQL compartilhado com RLS; ADRs iniciais
- Tecnologias: .NET SDK 10.0.302; runtime 10.0.10; PostgreSQL local 18.6; Git 2.54.0
- Tecnologias rejeitadas por ora: Redis, brokers, Kubernetes, microservices, motor de busca externo
- Tarefa atual: gerador Vegeta 12.13.0-orbis.1 reproduzível/auditado, sete testes externos; 12 checks do gate aprovados
- Problemas: Docker ausente; acesso de rede do shell exige elevação; infraestrutura de produção não contratada
- Próxima ação: implementar host Performance em processo separado para baseline Small, sem alterar a segurança da API
- Próximas cinco ações: host Performance separado; telemetria de recursos; baseline ≥5 min; interpretar gargalos; CI remoto

## Reconstrução de contexto

Ler README, este arquivo, next-actions, decision-log, product/backlog e architecture/technology-evaluation. Inspecionar `git status` e `git log --oneline -15`. Não interpretar itens planejados como implementados.
