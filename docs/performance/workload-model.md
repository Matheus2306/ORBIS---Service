# Modelo de workload — versão 1

Data: 2026-09-17. **Hipóteses de planejamento, não telemetria de clientes. Nível 1 em elaboração; 1M não validado.**

## Definições e equações

Registrado é conta global persistida. MAU/DAU são pessoas distintas ativas no mês/dia. Ativo na janela fez pelo menos uma ação útil naqueles 15 minutos. Simultâneo é usuário em jornada naquele instante; sessão autenticada aberta não implica conexão TCP nem request em voo.

Janela T=900 s, permanência média D=180 s, frequência f=6 requests/min por usuário em jornada. Com chegadas uniformes, concorrência C=A×D/T; RPS=C×f/60. Reads=0,8×RPS; writes=0,2×RPS. Eventual burst de 2× deve ser testado separadamente, sem reduzir segurança ou payload. Little: requests em voo ≈RPS×latência **média** em segundos, não p95. A 20 mil RPS e média hipotética 0,2 s são 4 mil requests em voo.

| Dimensão | Operação nominal (hipótese) | Evento de pico (meta) |
|---|---:|---:|
| Registrados globais | 1.000.000 | pelo menos 1.000.000 |
| MAU | 300.000 | pelo menos 1.000.000 no mês do evento |
| DAU | 100.000 | pelo menos 1.000.000 no dia do evento |
| Pessoas ativas em 15 min | 30.000 | 1.000.000 |
| Simultâneos em jornada (média) | 6.000 | 200.000 |
| Requests por usuário/min em jornada | 6 | 6 |
| Requests/s | 600 | 20.000 |
| Reads/s | 480 | 16.000 |
| Writes/s | 120 | 4.000 |
| Sessões autenticadas abertas (limite do cenário) | 100.000 | 1.000.000 |
| Jobs/s (0,5 por write, hipótese) | 60 | 2.000 |
| Eventos/s (1,5 por write, hipótese) | 180 | 6.000 |
| WebSockets | 0 no corte inicial | 0 até necessidade demonstrada |

Uma conta pode ter vários memberships; não confundir quantidade de memberships com pessoas. Sessões e conexões HTTP/2 devem ser medidas separadamente. Jobs/eventos são dimensionamento futuro; não serão contados como funcionalidades já implementadas.

## Mix e distribuição

30% lista paginada, 35% detalhe, 15% indicadores; 5% criação, 10% transições, 5% agenda/disponibilidade. Até implementá-los, um teste parcial deverá declarar seu mix real e não reivindicar equivalência ao workload completo. Tokens válidos, memberships reais, RLS, audit, idempotência, índices e payloads de produção habilitados. Uploads: cenário adicional de 1% de usuários ativos enviando 2 MiB na janela (≈22,2 MiB/s no pico); não diluir bytes no RPS de JSON.

10 mil tenants no Large; distribuição inicialmente 80/20 e cenário hot tenant com 30% de toda carga. No noisy neighbor, tenant ofensivo tenta 10× sua alocação enquanto os demais mantêm carga constante. Comparar p95/p99 e sucesso dos demais, inclusive durante 429 e recuperação.

## Sensibilidade e capacity planning

Para A=1M: D=1 min e f=2 →2.222 RPS; D=3/f=6 →20.000; D=5/f=12 →66.667. A hipótese escolhida exige pesquisa de comportamento e revisão, não conveniência do gerador. Burst 2× do cenário base=40 mil RPS.

Cada request pode causar consulta de membership + consulta de negócio + configuração transacional; writes ainda geram auditoria/WAL. Modelar consultas/s, CPU, IOPS e conexões a partir de traces reais; RPS HTTP não é TPS do banco. Teto dos pools: N_API×pool_API + N_worker×pool_worker + reserva_operacional ≤ conexões úteis medidas. Réplicas de aplicação não aumentam capacidade do primary.

Instâncias necessárias: ceil(RPS_meta / RPS_sustentável_medido_por_instância / utilização_alvo). Utilização alvo inicial 0,6, sujeita a ensaio. Depois validar ganho com N e perda de uma instância/zona. Nenhum valor de RPS/instância foi medido ainda; não há número de instâncias certificado.

## Validação em seis níveis

1. Modelo e arquitetura; 2. benchmarks locais; 3. carga controlada; 4. distribuída; 5. infraestrutura equivalente; 6. workload completo do evento de 1M, com mix/tenant skew/dataset/falhas e gerador saudável. Apenas 6 permite o rótulo 1M SCALE VALIDATED.

Estado inicial: VALIDADO: nenhum usuário/RPS; PROJETADO: 1M ativos em 15 min, 200k em jornada, 20k RPS sustentados e burst 40k; NÃO VALIDADO: capacidade, pico, soak, autoscaling, HA e recuperação. Manter estes campos atualizados em relatórios, preservando resultados históricos.
