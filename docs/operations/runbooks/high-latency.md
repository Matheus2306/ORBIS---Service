# Latência elevada

Separar rota/read/write/tenant skew e gerador/cliente. Comparar p95/p99 com baseline. Verificar CPU, GC, pool waits, locks, query time, I/O e WAL antes de alterar capacidade. Capturar query plans sanitizados; não registrar payload/PII. Limitar consultas caras e jobs por concorrência.

Se iniciou após deploy, aplicar rollback compatível. Escalar API somente se banco tiver headroom e soma dos pools continuar segura. Recuperação: budgets por rota voltam por janela estável e fila drena; conferir integridade. Não declarar resolvido porque média caiu.
