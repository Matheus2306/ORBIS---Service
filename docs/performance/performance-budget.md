# Budgets e gates

| Operação | p95 nominal | p99 nominal | p95 pico | p99 pico | Limite |
|---|---:|---:|---:|---:|---|
| Detalhe de ordem | 300 ms | 800 ms | 1.000 ms | 2.000 ms | DTO sem anexos embutidos |
| Lista de ordens | 300 ms | 800 ms | 1.000 ms | 2.000 ms | keyset; default 25, máximo 100 |
| Criar solicitação/ordem | 500 ms | 1.200 ms | 1.000 ms | 2.000 ms | body até 32 KiB; idempotência |
| Aceitar/iniciar/concluir | 500 ms | 1.200 ms | 1.000 ms | 2.000 ms | versão e conflito explícitos |
| Exportação futura | 500 ms para aceitar job | 1.200 ms para aceitar job | SLO próprio | SLO próprio | não executar relatório ilimitado no request |

Erros de servidor+timeouts <0,1% nominal e <1% pico; 5xx reportados separados. 4xx maliciosos previstos excluídos de disponibilidade funcional, mas reportados por classe. 429 em tráfego legítimo conta como demanda não atendida, nunca escondido como teste bem-sucedido. Endpoint pesado possui série própria.

Regressão >10% de p95/p99/throughput sobre baseline **comparável** bloqueia o gate correspondente até investigação. Comparabilidade exige mesmo hardware, dataset, segurança, mix, duração, aquecimento e gerador sem saturação. Ruído requer repetição controlada, não atualização automática do baseline. Não aprovar performance com amostra pequena nem comparar Debug com Release.

Histórico em reports, com commit, manifesto do dataset, configurações sanitizadas, distribuição de erros e artefatos. Primeiro smoke só detecta falhas grosseiras; não certifica SLO. Release importante exige matriz relevante mais teste prolongado antes de promoção.
