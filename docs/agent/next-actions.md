# Próximas ações

1. Escolher/experimentar gerador HTTPS sem ignorar certificados. Gate local validado com 119 testes e 21 regras; Kestrel/VerifyFull e métricas nativas disponíveis. Definir host Performance separado e coleta de recursos antes da carga. Windows não tem k6 nem distribuição WSL; Java existente é antigo/32 bits, não adotado.
2. Medir baseline Release (warmup + ≥5 min) com autenticação, mix de negócio, isolamento e métricas do gerador/servidor; separar limitações locais. k6 é candidato; nada instalado ainda.
3. Investigar planos de escrita e custos de transação/índices se o baseline mostrar gargalo; não converter tempos SQL isolados em capacidade de API.
4. Levar o gate local para CI remoto, acrescentar imagem/scans/performance smoke e grants operacionais. Não declarar CI verde por execução local.
5. Definir quotas/retenção, integração IdP e portais; evolução de carga depende de evidência anterior.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
