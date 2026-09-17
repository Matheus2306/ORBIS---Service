# Estratégia de performance

Ferramenta candidata: k6 para HTTP com arrival rate, thresholds e execução distribuível. Alternativas NBomber (ecossistema C#), Locust (Python), JMeter/Gatling (runtime adicional). Escolha k6 pelo workload aberto e portabilidade de cenários, a validar ao instalar gerador. BenchmarkDotNet reservado a CPU/allocations comprovadamente críticos; nenhum microbenchmark substitui aplicação+banco.

| Cenário | Procedimento reproduzível | Aceite / evidência |
|---|---|---|
| Baseline | Release, Small, warmup 30s, medir ≥5 min | percentis por endpoint, RPS alcançado, integridade |
| Load | taxa nominal por 15 min | budgets e estabilidade de recursos |
| Step | patamares de 5 min + rampas; até saturação | localizar knee point com p95, fila, CPU, pool, I/O |
| Stress | exceder knee point em incrementos limitados | 429/503 controlados, sem corrupção, recuperação |
| Spike | A=10k→50k→100k→250k→500k→1M | RPS=200→1k→2k→5k→10k→20k; workload completo |
| Soak | 1h→4h→8h→24h | tendências de RAM, handles, GC, conexões, filas |
| Scalability | mesmas condições com 1/2/4 instâncias | eficiência e mudança do gargalo, pools globais |
| Failover | retirar uma instância/banco em ambiente dedicado | indisponibilidade, reconexão, integridade |
| Recovery | restaurar componente e manter carga | tempo de retorno, backlog, duplicidades |
| Hot tenant | 30% das ações em um tenant enterprise | índices, locks, justa alocação e integridade |
| Noisy neighbor | um tenant em 10× quota | impacto nos demais e eficácia dos limites |

As durações são planos, não execuções concluídas. Failover/stress somente em destino Performance explicitamente configurado, jamais URL de produção inferida.

Coletar: RPS/throughput, p50/p90/p95/p99/max, 5xx/4xx/429/timeouts, CPU/RAM/GC/allocations/threads/conexões da API, pool/CPU/locks/query time/WAL/vacuum do banco, rede/disco do host e CPU/RAM/rede/dropped iterations do gerador. Cache e queue: N/A até existirem; depois hit ratio/depth/age. Histograms precisam de buckets que cubram os budgets.

Gerador: taxa solicitada versus alcançada, CPU sustentada <70% inicialmente, memória/rede sem saturação, relógio sincronizado, sem dropped iterations para aceitar a meta. Separar geradores/API/banco no nível 4+. Saturação do gerador invalida **a conclusão de capacidade da aplicação**; conservar o relatório como diagnóstico de teste inválido. Aumentar VUs não corrige CPU/rede insuficiente.

Profiling após gargalo: dotnet-counters/trace ou profiler disponível, pg_stat_statements, EXPLAIN (ANALYZE, BUFFERS), waits/locks e I/O. Definir hipótese, capturar antes, mudar uma variável, medir depois, manter testes funcionais. Nenhuma otimização sem par antes/depois.
