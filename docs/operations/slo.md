# SLO / SLI

Disponibilidade alvo: **99,95% mensal** para criar, consultar e transicionar ordens. Em mês de 30 dias: 43.200 min ×0,0005 = **21,6 min** de error budget por tempo; SLI principal por requisição também deve ser medido (bons/ elegíveis). Não trocar denominadores para ocultar incidentes. 4xx esperados por entrada maliciosa não entram; timeouts, 5xx e 429 de tráfego legítimo entram como falhas.

Latência por endpoint e classe read/write conforme performance-budget. Janelas 5 min/1h/6h/30 dias; histogramas, não médias. Throughput: taxa aceita e concluída; saturação: fila, pool e waits. Jobs futuros: p95 queue delay alvo inicial 30s para notificações, com SLO separado de envio externo.

Alertas candidatos: burn rate ≥14,4 em 1h+5min, ≥6 em 6h+30min; p99 fora do budget sustentado; fila crescendo; pool exaurido; 5xx/timeouts; falhas de autenticação incomuns e sinais de acesso cruzado. Só ativar paging após validar ruído e dono do alerta. Não usar CPU isoladamente como incidente.

Implementação: logs JSON, trace/correlation ID, ASP.NET/HTTP/runtime/DB metrics; avaliar OpenTelemetry para exportação ao backend escolhido. Labels por rota/status/operação, tenant apenas onde acesso e cardinalidade forem controlados. Health live sem dependências; ready com timeout do banco e schema/serviços necessários, sem detalhes públicos.

RPO≤5 min e RTO≤30 min são objetivos até restore medido. Relatório deve mostrar timestamp de último dado recuperado, início/fim e verificação funcional. SLO não é SLA contratual automaticamente. Nenhuma disponibilidade de produção foi medida.
