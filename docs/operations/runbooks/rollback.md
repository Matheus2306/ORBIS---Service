# Rollback

Pré-condições de produção: artefato anterior identificado por digest, config externa compatível, schema expandido aceita N/N+1 e procedimento do destino testado. Esses artefatos ainda não existem.

Parar promoção, redirecionar tráfego/canary para versão anterior, drenar requests, observar health/SLIs e executar smoke de tenant A/B. Não executar Down que destrua colunas/dados no incidente. Backfill possui pausa/retomada; contract schema só após janela de retenção de versão anterior. Se schema incompatível, forward fix ou restauração planejada com perda explícita, nunca rollback cego.
