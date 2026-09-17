# Dados sintéticos reproduzíveis

| Nível | Usuários globais | Tenants | Ordens/operações persistidas |
|---|---:|---:|---:|
| Small | 1.000 | 50 | 10.000 |
| Medium | 100.000 | 1.000 | 1.000.000 |
| Large | 1.000.000 | 10.000 | 10.000.000 |
| Historical | ≥Large | ≥Large | 100.000.000 eventos/históricos quando implementados |

Gerador deve produzir manifesto com seed, versão de schema/gerador, quantidades reais por tabela/status/tenant, cardinalidades e hash da configuração. Sem dados reais. IDs determinísticos por seed/namespace; timestamps fixos UTC e distribuição por 24 meses. Memberships múltiplos sem duplicar usuários globais; distribuição skewed reproduzível e um hot tenant com 30% das ordens. Também manter perfil uniforme para comparação.

Popular em lotes limitados/COPY; não manter milhões de entidades rastreadas. Validar FKs/checks/contagens, executar ANALYZE e capturar tamanho de tabelas/índices; não desabilitar constraints para declarar benchmark realista. As credenciais do gerador não são as da API. Destino precisa ser cluster de teste dedicado; comando recusa banco não marcado como Performance/Testing. Sem TRUNCATE de bases fornecidas implicitamente.

Estimativa inicial ilustrativa de capacidade de disco: 1–4 KiB por ordem incluindo índices significa 10–40 GiB para 10M, antes de audit/anexos/WAL/backups. Medir tamanho Small e refinar; não usar estimativa como evidência. Grandes datasets ficam fora do Git. Historical ainda depende do schema de histórico e não deve fingir tabela inexistente.
