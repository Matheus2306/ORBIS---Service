# Aumento de erros

Classificar 5xx, timeout, 401/403 e 429 por rota. Correlacionar com deploy, dependência, expiração de segredo e mudança de tráfego. Preservar trace IDs e logs sanitizados. Suspender rollout; reduzir carga de tarefas adiáveis se proteger operação crítica.

Erro de autorização cruzada exige incident-response. Erro de banco exige database-unavailable. Retentar apenas comando com chave idempotente e budget; falha de validação não recebe retry. Recuperação exige sucesso funcional, integridade e burn rate normalizado, não apenas processo vivo.
