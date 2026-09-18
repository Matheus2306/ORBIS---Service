# Testes de carga

Estratégia e matriz em docs/performance/test-strategy.md. Scripts serão introduzidos junto aos endpoints reais e credenciais de teste; não medir health checks e chamar de capacidade de negócio. Não versionar tokens. Relatórios sanitizados em docs/performance/reports; dumps de telemetria locais em .artifacts.

## Planos SQL locais

Após build Release, `./scripts/capture-query-plans.ps1 -Profile Uniform` (ou HotTenant) cria Small seed 42 em cluster descartável, aplica os grants do runtime, captura consultas e encerra PostgreSQL. O manifesto do dataset e `query-plans.json` ficam no mesmo diretório de evidências. O probe recusa credencial privilegiada, RLS inativa e volumes diferentes do Small esperado. A preparação usa outra credencial; as medições abrem conexão como `orbis_runtime`.

`tools/Orbis.QueryProbe` intercepta os comandos SQL/valores/tipos produzidos pelos leitores EF reais de diretório, membership, detalhe e lista; não altera código de produção. Mede os três perfis de autorização: despachante, cliente e prestador. Para a página em 80% do conjunto autorizado, executa a alternativa offset somente na ferramenta e exige IDs/hasMore iguais à página keyset da aplicação. Limite 25 (consulta busca 26). RLS e filtro por recurso continuam ativos.

Cada uma das 16 consultas possui um aquecimento adicional e 30 execuções seriais de `EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)`. A preparação/captura já aqueceu parte dos dados. Arquivo inclui SQL/params sintéticos, primeiro plano medido, amostras de planning/execution, buffers compartilhados/temporários e percentis nearest-rank da execução no servidor. Com 30 amostras, p99 coincide com máximo; não usar como evidência de cauda sob carga.

`EXPLAIN ANALYZE` executa e instrumenta a consulta, introduzindo overhead; consultar [PostgreSQL 18](https://www.postgresql.org/docs/18/using-explain.html). Probe usa transação read-only e timeout de 5s para os EXPLAINs. Tempos não incluem JWT, HTTP, serialização, todas as consultas do request, rede externa ou filas de concorrência. Não medem RPS, knee point, escala ou SLO de endpoint. Não substituir carga HTTP ou gate de regressão por esses números. Sem alterações de índices/cache até interpretar evidência e medir o workload relevante.
