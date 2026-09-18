# Próximas ações

1. Obter query plans de detalhe/lista antes de escolher índices adicionais; comparar keyset e offset em consultas equivalentes sobre Small.
2. Investigar planos de escrita e custos de transação/índices com medição apropriada; não converter duração de importação em throughput da aplicação.
3. Escolher/configurar gerador de carga e medir baseline Release com autenticação, isolamento e métricas do gerador/servidor; separar limitações locais.
4. Automatizar gates em CI e grants operacionais; manter artefatos imutáveis e scans fail-closed.
5. Definir quotas/retenção, integração IdP e portais; evolução de carga depende de evidência anterior.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
