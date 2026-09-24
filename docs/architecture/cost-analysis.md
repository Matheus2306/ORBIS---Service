# Modelo de custo — sem cotação comercial

Nenhuma infraestrutura contratada nem capacidade por instância medida. Valores monetários finais estão **indeterminados**; apresentar preço mensal fechado agora confundiria hipótese com evidência. O modelo abaixo estima unidades faturáveis e permite inserir tarifas verificadas do provedor escolhido.

Escala proporcional ao evento do workload, não ao simples cadastro:

| Ativos em 15 min | RPS projetado | Writes/s | Uploads a 1%×2 MiB | Tráfego JSON na janela a 4 KiB/response |
|---|---:|---:|---:|---:|
| 10.000 | 200 | 40 | 200 MiB | ~0,687 GiB |
| 100.000 | 2.000 | 400 | 2.000 MiB | ~6,87 GiB |
| 1.000.000 | 20.000 | 4.000 | 20.000 MiB | ~68,7 GiB |

São estimativas por evento; tráfego mensal depende de frequência e atividade fora do evento. Anexos/downloads/CDN, replicação, TLS e overhead não incluídos nessas linhas. Registered users sozinhos não determinam consumo.

Fórmula mensal: N_API×preço_instância + N_ADMIN×preço_instância_administrativa + DB_primary+standby+IOPS+WAL + storage_GB×tarifa + egress_GB×tarifa + ingestão/retenção_observabilidade + backups_GB×tarifa + LB/DNS/edge + IdP_MAU + workers. Host administrativo acrescenta processo/deployment e até8 conexões por instância, além de20 por instância comum. Cache/queues externos=0 enquanto não adotados. N_API/N_ADMIN dependem do benchmark e reserva de falha; custo adicional ainda não medido. DB segue carga/IOPS/pool medidos; HA duplica parte do custo base mesmo com poucos usuários.

Orçamento observável: medir custo por tenant ativo, por mil ordens, por GB e por evento de pico. Dados de logs podem superar dados de negócio; sampling/retention não podem eliminar trilha de auditoria exigida. Comparar três ofertas com região/moeda/data, egress, backup, suporte e compromissos antes da escolha. Fase seguinte preenche preços e intervalos após baseline; este documento **não é uma cotação nem evidência de viabilidade econômica**.
