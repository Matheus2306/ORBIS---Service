# Evidência de diretório e API autenticada

Data: 2026-09-17. Revisão: incremento que introduz este relatório, sobre fdb089c. Windows x64, SDK 10.0.302, runtime ASP.NET 10.0.10, EF 10.0.12/Npgsql EF 10.0.3 e PostgreSQL 18.6. Ambiente local; não equivalente à produção.

Build Release: zero warnings/erros. Format verify: aprovado. Execução `./scripts/test-postgres.ps1`: **24 testes de domínio/arquitetura + 30 de integração/HTTP = 54 aprovados; zero falhas/ignorados**. PostgreSQL real e role restrita; JWT RSA real, com discovery estático no fixture. Cluster loopback com SCRAM, max_connections 40, shared_buffers 64MB, pool runtime 12/admin 5, API com comando 5s. Integração durou aproximadamente 9s; isso não mede latência ou capacidade.

Cobertura adicional: host exato verificado, tenant/identidade global e memberships, permissões distintas da mesma pessoa em A/B, IDOR entre tenants e entre clientes do mesmo tenant, headers de tenant/proxy sem autoridade, token sem assinatura válida/issuer/audience/lifetime/tipo ou ausente, revogação com o mesmo token, suspensão do tenant. Migração parte de schema anterior populado, preserva ordens e mantém legado sem acesso implícito. Runtime não pode ativar tenant. Guard recusa cada privilégio INSERT/UPDATE/DELETE/TRUNCATE em memberships; readiness detecta drift/recuperação após TTL e responde a probes concorrentes. Testes prévios de RLS, pooling e 100 versões concorrentes permanecem verdes.

Artefatos locais: `.artifacts/postgres/7689c1ea175142c6ae64ddafd3898653/test-results/`. SHA-256 TRX unitário: `874CEDCE4D1E0B5A563F225750AF99FA20CE5F19D9E17E8054865FE4DCC8417C`; integração: `C7BC52E3DB0D4AD087B8FDB6EE017605D69C12F53D89D1BAF6A534A1C7428A44`. Cluster encerrado. Artefatos ignorados pelo Git, sem sanitização automática; revisar antes de compartilhar.

Falhas intermediárias registradas: validação de configuração antecipada impedia o host de testes de fornecer configuração; movida para Options/DI com ValidateOnStart. O formatador converteu GRANT interpolado em parâmetro inválido; teste passou a usar allowlist de SQL constante. Execução c28d37d50a2e450b9f3b2dfb4334c5a0 tinha quatro falhas e não é evidência de aprovação. Nenhum teste foi removido ou ignorado.

Revisão estática independente de arquitetura concluída, seguida de ajustes de grants/readiness e reconciliação do threat model. Gitleaks 8.30.1: working tree e três commits anteriores sem leaks. NuGet transitivo: feed oficial sem advisories reportados. EF `has-pending-model-changes`: nenhum nos dois contextos. Não equivale a SAST completo, DAST, segurança de IdP real ou scan de imagem.

**VALIDADO:** comportamento funcional acima em fixture pequeno. **PROJETADO:** workload documentado de 1M. **NÃO VALIDADO:** IdP operacional, RPS/p95/p99, Small dataset, noisy neighbor, load/stress/spike/soak, migração volumosa, HA, backup/restore ou produção. Nenhuma classificação de escala é concedida.
