# Próximas ações

1. Instrumentar sinais nativos antes da carga; Kestrel real/HTTPS em Performance e PostgreSQL VerifyFull passaram em 118 testes. Nomear pool sem connection string e verificar atributos HTTP/DB. Confirmar ferramenta/telemetria antes de executar carga.
2. Medir baseline Release (warmup + ≥5 min) com autenticação, mix de negócio, isolamento e métricas do gerador/servidor; separar limitações locais. k6 é candidato; nada instalado ainda.
3. Investigar planos de escrita e custos de transação/índices se o baseline mostrar gargalo; não converter tempos SQL isolados em capacidade de API.
4. Automatizar gates em CI e grants operacionais; manter artefatos imutáveis e scans fail-closed.
5. Definir quotas/retenção, integração IdP e portais; evolução de carga depende de evidência anterior.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
