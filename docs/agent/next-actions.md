# Próximas ações

1. Entregar criação idempotente com audit na mesma transação; retry paralelo e payload divergente não podem duplicar efeito.
2. Expor transições de ordens com versão esperada, permissão/recurso, idempotência e audit; testes HTTP de concorrência.
3. Entregar lista keyset com autorização no SQL, limite/cursor validados e testes contra vazamento/duplicação.
4. Gerar Small dataset e medir baseline Release, inclusive gerador/servidor e query plans.
5. Automatizar gates em CI, grants operacionais e provisionamento controlado; depois integrar IdP e portais.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
