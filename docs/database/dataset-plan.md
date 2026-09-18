# Dados sintéticos reproduzíveis

| Nível | Usuários globais | Tenants | Ordens/operações persistidas |
|---|---:|---:|---:|
| Small | 1.000 | 50 | 10.000 |
| Medium | 100.000 | 1.000 | 1.000.000 |
| Large | 1.000.000 | 10.000 | 10.000.000 |
| Historical | ≥Large | ≥Large | 100.000.000 eventos/históricos quando implementados |

Implementação: `tools/Orbis.DataGenerator`, independente do binário da API. Receita v1, seed 42 por padrão, perfis Uniform e HotTenant (tenant 0 recebe 30% das ordens). Small/Medium/Large possuem receitas; execução e verificação de volumes são evidências separadas. Historical ainda não implementado.

Manifesto JSON registra configuração/hash, versões das migrations/PostgreSQL, contagens reais por tabela/status/tenant, tamanho total de cada tabela com índices/TOAST e SHA-256 do conteúdo persistido. O hash usa COPY binário ordenado; comparar na mesma versão de PostgreSQL/formato, sem presumir portabilidade entre majors. Horário, duração e tamanhos físicos podem variar, mesmo com conteúdo idêntico. Duração de importação/verificação não é throughput da API.

IDs de ordem são UUIDv7 determinísticos; demais IDs usam SHA-256 com namespace/seed e UUIDv8. Datas distribuídas entre setembro/2024 e agosto/2026, UTC, sem depender do relógio do teste. Descrições sintéticas de comprimentos variados; nenhum dado real. Cada tenant tem um despachante, 20% prestadores e os demais clientes. Um prestador de cada tenant também é cliente do próximo: Small contém 1.000 usuários globais e 1.050 memberships. Sem duplicação de identidade; issuer/subject e hosts `.test` são exclusivamente sintéticos.

Perfil uniforme Small: 200 ordens/tenant; 50% Requested, 10% Assigned, 10% Accepted, 10% InProgress, 15% Completed, 5% Cancelled. No perfil concentrado, tenants residuais recebem divisão inteira do restante e podem ter proporções discretas diferentes; manifesto informa valores reais. Cada ordem possui criação, auditoria por versão e recibos das transições correspondentes. Cancelamento sintético ocorre antes de atribuição. Esses dados não representam pesquisa de comportamento de clientes.

Importação streaming com COPY binário tipado, FKs/checks/triggers ativos, sem rastreamento EF de entidades. Uma transação com lock exclusivo nas nove tabelas cobre importação, coerência de históricos, ANALYZE, contagens e hashes. Essa transação grande é apropriada apenas ao preparo de banco isolado; volume Large exige medir WAL/disco/tempo antes de executá-la. Não é estratégia de importação online ou de migração de produção.

COPY FROM não aplica políticas RLS como uma operação comum da API. O gerador exige role privilegiada separada, mas mantém ENABLE/FORCE RLS; testes posteriores usam runtime restrito. A [documentação PostgreSQL](https://www.postgresql.org/docs/18/sql-copy.html) descreve restrições de COPY/RLS, e o [Npgsql](https://www.npgsql.org/doc/copy.html) exige tipos corretos e Complete para confirmar a cópia.

Destino deve ter Host exatamente `127.0.0.1`, conexão de fato loopback e nome `orbis_perf_*` ou `orbis_test_*`. Tabelas desconhecidas ou qualquer dado de negócio pré-existente impedem a execução antes das migrations; revalidação sob locks impede importações simultâneas. Nome/prefixo não substituem um cluster dedicado. Nenhum DELETE/TRUNCATE/DROP é executado. Falha durante a carga reverte todos os dados de negócio; migrations anteriores permanecem. Falha de gravação do manifesto pode deixar dataset confirmado sem evidência publicada: verificar o ambiente e gerar outro cluster, nunca sobrescrever dados.

## Execução local

Após restore/build Release, com PowerShell 7 e PostgreSQL 18:

```powershell
./scripts/generate-dataset.ps1 -Level Small -Profile Uniform -Seed 42
./scripts/generate-dataset.ps1 -Level Small -Profile HotTenant -Seed 42
```

Cada execução provisiona cluster exclusivo SCRAM, gera manifesto em `.artifacts/postgres/<run>/dataset-manifest.json` e encerra PostgreSQL. Artefatos locais são ignorados; revisar logs antes de compartilhar. `scripts/with-local-postgres.ps1` concentra o ciclo de vida usado também pelos testes. A CLI pode usar banco local isolado fornecido explicitamente via `ORBIS_DATASET_CONNECTION`; não passar credenciais por argumentos. Caminho de manifesto deve ser novo. Ctrl+C propaga cancelamento à carga.

Estimativa inicial ilustrativa de capacidade de disco: 1–4 KiB por ordem incluindo índices significa 10–40 GiB para 10M, antes de audit/anexos/WAL/backups. Medir tamanho Small e refinar; não usar estimativa como evidência. Grandes datasets ficam fora do Git. Historical ainda depende do schema de histórico e não deve fingir tabela inexistente.
