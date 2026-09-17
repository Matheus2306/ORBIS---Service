# ADR-006 — processamento assíncrono progressivo

Status: planejado, nenhum worker implementado inicialmente. Problema: notificações/exports não podem alongar transações HTTP; precisam sobreviver a restart. Requisitos: durabilidade, idempotência, backpressure, tenant scope e retries limitados.

Alternativas: BackgroundService/Channels resolvem execução efêmera, não durabilidade; tabela de jobs oferece transação local; brokers acrescentam infraestrutura e throughput independente. Decisão candidata: jobs PostgreSQL com lease, tentativas, next_attempt, chave de deduplicação e estado terminal; worker BackgroundService com concorrência limitada e claim via SKIP LOCKED. Payload mínimo, sem tokens. Outbox somente quando publicação externa precisar de atomicidade com domínio; não adicionar antes desse fluxo.

Evidência: requisito de notificação confiável; sem benchmark de fila. Consequências/riscos: polling e disputa no banco, entregas at-least-once e leases vencidos. Testar duplicidade, crash antes/depois de efeito, lease expirada e recuperação. Métricas: queue delay, depth, age, attempts, terminal failures. Reconsiderar broker após medir atraso/lock/CPU e necessidades de fanout/replay. Retirada futura exige drenar jobs e manter deduplicação.
