# Próximas ações

1. Persistir diretório Tenant/Domain e identidade global issuer+subject; roles de control plane separadas.
2. Resolver host por registro confiável e cruzar JWT válido, tenant ativo e membership ativo.
3. Entregar detalhe/lista de ordens com autorização por recurso e paginação limitada; testes HTTP maliciosos.
4. Entregar comandos com idempotência e audit na mesma transação; testar retries/concorrência reais.
5. Gerar Small dataset e medir baseline Release antes de otimizar; automatizar os gates executáveis em CI.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
