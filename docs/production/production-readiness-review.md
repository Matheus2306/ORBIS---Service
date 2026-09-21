# Production readiness review

Data: 2026-09-21. Resultado: **NO-GO**. Núcleo de API testado localmente, sem produção implantada. Não atribuir Production Ready ou Production Scale Validated.

| Gate | Estado inicial | Evidência necessária |
|---|---|---|
| Produto/jornadas | desenho inicial | três experiências completas e aceite |
| Build reproduzível/CI | build local Release aprovado; CI pendente | pipeline verde e artefato imutável |
| Isolamento/autorização | PostgreSQL, ciclo de ordens, dataset e HTTPS local testados; restante pendente | 119 testes no relatório de métricas nativas; novos componentes exigem testes próprios |
| Security review | pendente | SAST/SCA/secrets/container/DAST + revisão |
| Migrations | upgrade local com legado aprovado; escala pendente | N/N+1, locks, dataset representativo |
| Backup/PITR | ausente | backup criptografado e WAL verificados |
| Restore | não executado | RPO/RTO medidos e checks de integridade |
| Observabilidade/alertas | emissão de métricas HTTP/TLS/pool testada; coleta e alertas pendentes | backend, recursos, traces e alerta recebido |
| Load/stress/spike/soak | não executados | relatórios e percentis por endpoint |
| HA/scaling | não implantado | failover e capacidade pós-perda de instância |
| Deployment/rollback | não executado | digest, config, IaC e ensaio |
| Segredos/identidade | provedor pendente | cofre, rotação, MFA e revogação |
| Runbooks/on-call | esboços sem exercício | responsáveis e simulado |
| Proteção de dados | requisitos iniciais | inventário, retenção e controles aprovados |
| 1M | somente modelo | nível 6 do workload, geradores saudáveis |

Não transformar pendência em aprovado por falta de infraestrutura. Produção inicial pode ter capacidade contratada menor, explicitamente validada, sem declarar 1M; isso não elimina o objetivo de evolução. Nenhuma exceção de isolamento ou vulnerabilidade crítica é permitida.
