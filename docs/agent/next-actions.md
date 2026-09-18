# Próximas ações

1. Gerar Small determinístico (1.000 usuários, 50 tenants, 10.000 operações), com manifesto, distribuição e dados sem PII.
2. Obter query plans de detalhe/lista/criação antes de escolher índices adicionais; comparar keyset e offset em consultas equivalentes.
3. Escolher/configurar gerador de carga e medir baseline Release com autenticação, isolamento e métricas do gerador/servidor; separar limitações locais.
4. Automatizar gates em CI e grants operacionais; manter artefatos imutáveis e scans fail-closed.
5. Definir quotas/retenção, integração IdP e portais; evolução de carga depende de evidência anterior.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
