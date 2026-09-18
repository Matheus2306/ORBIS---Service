# Preparação de dataset Small — não é teste de carga

2026-09-18, incremento sobre `924801f`. Receita v1, seed 42, executada em Release no Windows x64/.NET SDK 10.0.302/PostgreSQL 18.6. Cada perfil em cluster novo: loopback, SCRAM, checksums, max_connections 40, shared_buffers 64MB, pool do importador 5. CPU disponível ao .NET: 8 processadores lógicos; hardware/RAM não inventariados. Sem containers/cloud/equivalência de produção.

Artefatos preservados: [Uniform](evidence/2026-09-18-small-uniform.json) e [HotTenant](evidence/2026-09-18-small-hot.json). Contêm migrations, contagens efetivamente lidas, hashes de conteúdo, tamanhos físicos e distribuição. Bancos e logs completos ficam ignorados em `.artifacts/postgres/6c131a70072d4c298b32727accbcc961/` e `fa4dfea258a84e50b57df57e2f6f0451/`; servidores encerrados.

| Métrica | Uniform | HotTenant |
|---|---:|---:|
| Usuários globais | 1.000 | 1.000 |
| Tenants | 50 | 50 |
| Memberships | 1.050 | 1.050 |
| Ordens | 10.000 | 10.000 |
| Ordens no tenant 0 | 200 | 3.000 |
| Audits | 22.500 | 22.325 |
| Recibos de criação / transição | 10.000 / 12.500 | 10.000 / 12.325 |
| Soma das nove tabelas + índices/TOAST | 20.275.200 bytes | 20.176.896 bytes |
| Importação + verificação no processo | 22,38s | 25,02s |

Tempos são observações únicas de preparação, incluindo migrations, constraints, hashing e ANALYZE. Não são baseline de throughput, comparação estatística ou percentis. Contagens do perfil concentrado são próprias ao arredondamento por tenant. WAL, catálogos, índices de migrations, espaço livre, logs, backups e arquivos do cluster não entram na soma. Não extrapolar linearmente o tamanho Small como capacidade operacional Large.

SHA-256 dos arquivos versionados (UTF-8/LF): Uniform `3B2FF522682EBFF452609CD5EF3BDBDFAF1FE2DC868614328234F8C0B0660C95`; HotTenant `FBE8909C62402B85CE54256D5421B4D9E4B4364033479F7BDFD309B94D1549CC`. Os testes geraram ainda duas bases independentes por perfil e compararam os nove hashes de conteúdo, todos idênticos. Evidência funcional: `docs/testing/reports/2026-09-18-synthetic-dataset.md`.

Reprodução: `scripts/generate-dataset.ps1 -Level Small -Profile Uniform -Seed 42`, repetir com HotTenant. Transação privilegiada de preparo; a API mantém sua role restrita. Nenhum índice novo, cache ou serviço externo foi introduzido.

**VALIDADO:** persistência e reprodução de 1.000 usuários/50 tenants/10.000 ordens, integridade e replay funcional. **RPS/usuários ativos simultâneos validados: nenhum.** **PROJETADO:** workload de pico 20.000 RPS, burst 40.000, para 1M ativos na janela de 15 minutos. **NÃO VALIDADO:** 1M ativos, Medium/Large, saturação, latência por endpoint, noisy neighbor, stress/spike/soak/failover. Próximo experimento: planos sob RLS, depois carga HTTP instrumentada.
