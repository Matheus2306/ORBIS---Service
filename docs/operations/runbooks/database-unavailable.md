# Banco indisponível

Confirmar por readiness, timeouts, pool e health do banco; distinguir DNS/TLS/credencial/exaustão/primary caído. Capturar timestamps e métricas. Preservar limites de concorrência; não multiplicar retries nem max_connections. API deve recusar temporariamente sem escrever parcialmente.

Em infraestrutura HA validada, acionar failover pelo procedimento versionado do provedor; ainda não disponível neste repositório. Revalidar role runtime/RLS, schema e reconexão. Confirmar transações idempotentes e integridade de ordens. Registrar duração e impacto no error budget. Se houver perda/corrupção, usar restore isolado e comparar antes de redirecionar tráfego.
