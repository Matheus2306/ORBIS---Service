# Observabilidade: sinais disponíveis e lacunas

2026-09-21. A API usa logs JSON, Problem Details com trace ID, health live/ready e instrumentos nativos ASP.NET Core/Npgsql. Há teste de emissão real por HTTPS; ainda não há backend de coleta, painel, retenção ou alerta operacional implantado. Instrumentação disponível não equivale a operação observável 24x7.

## Métricas nativas

| Meter / instrumento | Uso | Evidência atual |
|---|---|---|
| Microsoft.AspNetCore.Hosting / http.server.request.duration | duração em segundos por rota, método, status, versão HTTP | teste Kestrel com 200/201/400/401/404 |
| Microsoft.AspNetCore.Hosting / http.server.active_requests | requisições em andamento | instrumento nativo; coletar durante carga |
| Microsoft.AspNetCore.Server.Kestrel / kestrel.connection.duration e kestrel.tls_handshake.duration | conexões e negociação TLS | emissão verificada em HTTPS local |
| Npgsql / db.client.operation.duration | duração de comando do cliente DB | emissão verificada; não equivale ao tempo total de request nem ao tempo exclusivo do servidor SQL |
| Npgsql / db.client.connection.count e db.client.connection.max | uso/ociosidade e teto do pool | coleta observável verificada; nenhuma conexão ocupada após a jornada |
| Npgsql / db.client.connection.npgsql.pending_requests e timeouts | espera/saturação do pool | instrumentos disponíveis; pressão e timeout ainda não exercitados com telemetria |
| Npgsql / db.client.operation.npgsql.bytes_read e bytes_written | tráfego do driver | disponíveis; não substituem medição de rede total |
| System.Runtime | CPU, memória, GC, allocations e threads | coleta externa por processo ainda pendente |

Fontes: [instrumentação ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/metrics/overview?view=aspnetcore-10.0), [Npgsql 10](https://www.npgsql.org/doc/diagnostics/metrics.html). O nome de alguns instrumentos Npgsql mudou na versão 10; fixar consultas/coletores junto às versões testadas. Não há pacote de observabilidade novo: System.Diagnostics.Metrics já está nas bibliotecas adotadas. OpenTelemetry/exportador será selecionado com o destino de coleta; não instalar Prometheus/Grafana/Collector por pressuposto.

## Fronteira de segurança e cardinalidade

O pool da API tem nome constante `orbis-runtime`. O default do Npgsql usa a connection string como nome, o que expõe metadados e cria séries variáveis; não depender de eventual remoção de senha pelo driver. O nome não deve receber TenantId, usuário ou credencial. Se houver pools de shards no futuro, nomes serão de um conjunto operacional limitado, com revisão de capacidade e acesso.

Os histogramas HTTP usam templates como `/v1/work-orders/{id:guid}`, nunca o ID concreto. O teste permite apenas atributos nativos conhecidos e procura token, senha, usuário/banco da conexão, caminho do certificado, host do tenant, IDs e marcador enviado no body/query. Valores sensíveis não são impressos nem se a asserção falhar. Duração DB inclui server.address/server.port: são metadados internos permitidos somente à operação. A ausência de vazamento é comprovada nesses instrumentos/casos, não constitui auditoria completa de logs/traces/exportadores.

Não publicar endpoint de métricas no portal ou disponibilizar acesso a tenants. Um exportador futuro precisa de autenticação, autorização de operadores, criptografia, retenção e revisão de atributos. Traces SQL/HTTP exigem revisão separada: não habilitar parâmetros de comandos, headers/token, body ou URL completa sem controles específicos.

## Coleta para a próxima medição

O teste usa MeterListener em memória, delimita a instância HTTP pelo IMeterFactory e o pool pelo nome fixo. O listener existe somente nos testes; não conserva amostras por request no processo de produção. Não calcula percentis de poucas requisições nem atribui RPS.

O baseline deverá separar processo da API e gerador e coletar System.Runtime + Hosting + Kestrel + Npgsql, incluindo CPU/RAM/rede/disco do host e do PostgreSQL. PG connections/locks/query time/WAL/vacuum precisam de coleta própria; o driver não informa CPU ou I/O do servidor. Gerador saturado invalida a conclusão de capacidade. Sincronizar relógios e correlacionar run/commit/dataset/configuração.

Preservar todas as classes de status. Health fica em série separada dos endpoints de negócio; não diluir erros com probes. 429 de tráfego legítimo conta como demanda não atendida. Configurar histogramas que permitam avaliar os budgets de 300/500/800/1.000/1.200/2.000 ms, além de p50/p90/p95/p99/max por endpoint. Alertas de SLO só serão marcados prontos após exercício de recebimento e runbook.
