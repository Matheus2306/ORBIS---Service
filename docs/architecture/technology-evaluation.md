# Avaliação de tecnologias

Fluxo exigido: problema → evidência → alternativas → experimento → medição → decisão → implementação → nova medição. Requisitos funcionais podem justificar adoção sem benchmark; ganhos de performance exigem medição. Nenhum produto é adotado por associação com escala.

| Tecnologia | Problema/evidência | Decisão e próximo experimento |
|---|---|---|
| .NET/ASP.NET/EF 10 | Stack obrigatória; SDK disponível | Adotar, fixar versões, build e testes Release |
| PostgreSQL 18/Npgsql 10 | Domínio relacional, transações e RLS | Adotar; provar isolamento/grants e baseline |
| Blazor Web App | Três portais e equipe .NET | Candidato SSR+WASM; medir primeiro fluxo contra alternativas |
| React/TypeScript | Ecossistema UX e independência | Alternativa; não adicionar segunda stack sem experimento |
| Cache/Redis | Gargalo ainda não observado | Não adotar; sem cache → memória → distribuído após profiling |
| BackgroundService/DB jobs | Notificação durável futura | Candidato; só implementar quando houver efeito assíncrono real |
| RabbitMQ/Kafka/Service Bus | Fanout/throughput não demonstrado | Adiar; medir fila PostgreSQL antes |
| S3/R2/Azure Blob | Arquivos privados futuros | Backend depende do destino/custo; fronteira privada definida |
| MinIO | Object storage local/gerido por nós | Não justificado sem requisito de autonomia operacional |
| PostgreSQL search | Busca estruturada inicial | Primeiro candidato; EXPLAIN em dataset representativo |
| Elasticsearch/OpenSearch | Busca externa sem requisito provado | Adiar; custo de sync/ACL/rebuild não justificado |
| OpenTelemetry | Exportação padronizada de sinais | Avaliar quando backend observável definido; métricas .NET primeiro |
| Prometheus/Grafana/Jaeger/Collector | Backend e consulta de telemetria | Nenhum selecionado; comparar serviço gerido e operação própria |
| Cloudflare/CDN/WAF | Fronteira pública e assets | Avaliar com domínio/hosting; API protegida não pode ser cacheada indiscriminadamente |
| Kubernetes/service mesh | Um serviço sem necessidade operacional demonstrada | Não adotar; plataforma gerida é candidata |
| Terraform/OpenTofu | Reprodução de infraestrutura | Escolher depois do provedor; não criar IaC fictícia |
| k6 | Testes HTTP com arrival rate e distribuição | Alternativa futura; não experimentado; primeira integração local usa Vegeta (ADR-013) |
| Vegeta 12.13.0-orbis.1 | CLI externo com taxa configurável e CA por processo | Adotado para transporte local; build Go 1.27.1/grafo corrigido reproduzível e 7 testes; baseline/recursos/distribuição pendentes |
| Go/govulncheck | Compilar e auditar o gerador sem runtime antigo vulnerável | Ferramentas isoladas da API, versões/checksums/locks fixados; nenhuma dependência Go no produto |
| BenchmarkDotNet | Microgargalo de CPU ainda não existe | Reservado a código onde profiling justificar |
| System.Diagnostics.Metrics | Provar sinais HTTP/TLS/pool antes de carga | Nativo já disponível; emissão/atributos testados, coletor/backend ainda não adotados |
| Gitleaks 8.30.1 | Gate de segredos exigido; regex isolada insuficiente | CLI MIT verificado, versão fixada no gate local; sem runtime/serviço novo; ausência falha a validação |

Para cada adoção adicional, ADR deve responder: gargalo, alternativa nativa .NET/PG, custo, falha nova, monitoramento, recuperação, testes/local/produção e remoção. Licenças, dependências transitivas e advisories devem ser registrados ao fixar pacotes. Versões do NuGet consultadas no feed oficial; não usar preview automaticamente.
