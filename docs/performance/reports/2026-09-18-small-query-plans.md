# Planos SQL Small sob RLS

Data: 2026-09-18. Código sobre `51b4597`, Release, SDK 10.0.302, EF 10.0.12/Npgsql EF 10.0.3, PostgreSQL 18.6, Windows x64 local compartilhado. SQL capturado dos leitores EF reais; somente a alternativa offset existe na ferramenta. Sem alterações no código de produção, índices ou segurança. Não é baseline HTTP nem validação de capacidade.

Execução: `scripts/capture-query-plans.ps1 -Profile Uniform`, depois HotTenant, sem execução concorrente da suíte/build. Dois clusters novos, cada um Small/seed 42, 10.000 ordens/50 tenants/1.000 usuários. Tenant observado: 200 ordens uniforme; 3.000 concentrado. Role `orbis_runtime`, RLS ativa, contexto por transação; probe recusa role privilegiada. Limite 25, SQL busca 26. Página profunda em 80% do conjunto autorizado; offset do despachante 160/2.400. IDs e hasMore da página offset conferidos contra keyset antes de medir.

Servidor: max_connections 40, shared_buffers 64 MiB, work_mem 4 MiB, effective_cache_size 4 GiB (estimativa do planner, não RAM alocada), random_page_cost 4, jit on, track_io_timing off. Sem I/O timing, CPU/RAM/GC/rede coletados nesta sondagem. Dados aquecidos pela importação, ANALYZE, hashes e consultas de captura; aquecimento adicional de um EXPLAIN por consulta. Sem controle exclusivo sobre atividades externas do host.

Cada célula abaixo é **p50 / p95 / p99 em milissegundos de execução SQL instrumentada no servidor**, nearest-rank, 30 amostras seriais por consulta/perfil. P99 é o máximo nessa amostra. Planejamento e buffers estão nos arquivos completos. Não incluem HTTP/JWT/serialização, a soma das consultas do request, transferência externa ou espera sob concorrência. Não comparar com SLO de endpoint.

| Consulta | Uniform | HotTenant |
|---|---:|---:|
| directory.lookup | 0,057 / 0,070 / 0,080 | 0,057 / 0,137 / 0,156 |
| dispatcher.detail | 0,030 / 0,047 / 0,047 | 0,024 / 0,031 / 0,046 |
| dispatcher.list.first | 0,039 / 0,086 / 0,180 | 0,036 / 0,047 / 0,053 |
| dispatcher.membership | 0,026 / 0,060 / 0,060 | 0,023 / 0,024 / 0,028 |
| dispatcher.list.deep.keyset | 0,053 / 0,125 / 0,128 | 0,066 / 0,117 / 0,300 |
| dispatcher.list.deep.offset | 0,150 / 0,252 / 0,252 | 1,967 / 2,507 / 2,797 |
| customer.detail | 0,043 / 0,072 / 0,075 | 0,026 / 0,037 / 0,129 |
| customer.list.first | 0,040 / 0,133 / 0,138 | 0,166 / 0,212 / 0,272 |
| customer.membership | 0,024 / 0,033 / 0,111 | 0,022 / 0,027 / 0,035 |
| customer.list.deep.keyset | 0,038 / 0,055 / 0,059 | 0,119 / 0,177 / 0,186 |
| customer.list.deep.offset | 0,039 / 0,052 / 0,070 | 0,179 / 0,305 / 0,307 |
| provider.detail | 0,032 / 0,042 / 0,047 | 0,023 / 0,030 / 0,034 |
| provider.list.first | 0,051 / 0,095 / 0,120 | 0,248 / 0,559 / 0,560 |
| provider.membership | 0,024 / 0,037 / 0,038 | 0,021 / 0,031 / 0,034 |
| provider.list.deep.keyset | 0,041 / 0,084 / 0,090 | 0,169 / 0,252 / 0,272 |
| provider.list.deep.offset | 0,051 / 0,065 / 0,067 | 0,302 / 0,425 / 0,527 |

## Interpretação e decisão

No HotTenant, keyset do despachante usa `IX_work_orders_tenant_id_created_at_id` e entrega 26 linhas sem sort; offset faz bitmap sobre 3.000 linhas, ordena e descarta 2.400. Plano inicial medido: 29 shared-hit blocks no keyset e 120 no offset. Uniform: keyset também usa índice temporal; offset ordena 200 linhas. Isso evidencia o trabalho evitado nessa paginação, sem demonstrar ganho de throughput da API.

Listas por cliente/prestador no tenant concentrado usam índices existentes de FK e ordenação: 200 linhas do cliente, 450 do prestador. Mesmo com seek, o plano filtra essas linhas antes do sort (40/90 sobrevivem). Possível ponto de crescimento a investigar em Medium/carga; ainda não é gargalo demonstrado. Detalhes HotTenant usam PK da ordem; diretório escolhe scans de tabelas de apenas 50 registros, o que não justifica forçar índices. Nenhum shared-read block ou escrita temporária nas 960 amostras; este ensaio não exercita disco frio.

Decisão: preservar índices existentes e seguir para baseline HTTP instrumentado. Não adicionar Redis, índices compostos adicionais ou particionamento com base somente neste dataset pequeno. Gargalo global, knee point, RPS, noisy neighbor e cauda de latência sob carga permanecem desconhecidos. Nenhum gate de produção/1M foi aprovado por estes números.

## Evidências e reprodução

Arquivos UTF-8/LF completos: [Uniform](evidence/2026-09-18-plans-uniform.json), SHA-256 `69DBE4712E73E7FD4CA39AD1AF285B91F59AE494181F9A0F77F1D9EC7F991DB1`; [HotTenant](evidence/2026-09-18-plans-hot.json), SHA-256 `B46A1756336786372537A639AD82E4F8D80730573428A0F3C56358046E207793`. Contêm parâmetros sintéticos, primeiro plano medido, 30 amostras/consulta e settings. Manifestos da mesma execução e logs em `.artifacts/postgres/82a593ae8a9d4cafbd0d52ecf4ce4463/` e `0a72f1bbe656484082a025a5f92f04ef/`; servidores encerrados. Receita/hash de conteúdo corresponde ao [dataset Small](2026-09-18-small-dataset.md).

Build Release/formatação aprovados, 108 testes verdes (mesmos casos, com assertions adicionais de segurança/equivalência dos planos), scans NuGet transitivo/Gitleaks sem achados reportados. TRX em `.artifacts/postgres/5635568fcfb34a5b8dd7abfb47bf456b/test-results/`: unitário SHA-256 `7D9214ED7A39E4D08BC1FE23E912C6D616447B323D29C4DCB999EA44014C69DF`; integração `5C2F999A807385784A90FE909F69BBA6975F8084CDD02DD3C759ED01D4FADD2B`. Integração ~96s não é benchmark.

Uma tentativa anterior de iniciar testes foi impedida pela revisão automática por falta de créditos do workspace; não executou nem gerou evidência. Após o usuário retomar, a revisão autorizou a execução acima. Não houve contorno de aprovação.
