# Próximas ações

1. Expor transições de ordens com versão esperada, permissão/recurso, idempotência e audit; testes HTTP de concorrência.
2. Gerar Small determinístico e obter query plans de detalhe/lista/criação antes de escolher índices adicionais.
3. Definir quotas/retenção do histórico e grants operacionais; criação já passou por concorrência/rollback reais.
4. Gerar Small dataset e medir baseline Release, inclusive gerador/servidor e query plans.
5. Automatizar gates em CI, grants operacionais e provisionamento controlado; depois integrar IdP e portais.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
