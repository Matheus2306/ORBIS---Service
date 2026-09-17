# ADR-011 — criação idempotente e audit atômico

Status: decisão para implementação do POST de solicitação. Problema: perda da resposta ou retry paralelo não pode gerar ordens duplicadas; criação precisa deixar trilha sem copiar texto pessoal para log. Requisito: mesma chave/tenant/ator/operação produz um efeito; payload diferente retorna conflito; permissão revogada continua negando replay.

Alternativas: deduplicar em memória falha com reinício/múltiplas instâncias; lock distribuído adiciona dependência sem necessidade; SERIALIZABLE global aumenta aborts; unicidade PostgreSQL e transação local atendem o efeito único. Decisão: tabela tenant-scoped de recibos de criação com PK (tenant,actor,key), namespace dedicado à operação v1. Chave UUID não vazia obrigatória no header. Fingerprint SHA-256 da descrição validada/normalizada, incluindo versão do contrato. Recibo imutável contém ID/data, sem descrição. Ordem + recibo + audit entram na mesma transação.

Concorrência: unicidade decide vencedor. Uma violação somente da constraint de idempotência faz rollback completo; um novo contexto/transação lê o recibo vencedor e revalida membership. No máximo uma nova tentativa local; não há retry cego de timeout/commit desconhecido. Cliente pode repetir com a mesma chave para descobrir um commit cuja resposta perdeu. Não usar resultado da tentativa abortada. Audit append-only por privilégio runtime, com ator/recurso/ação/data; sem texto do pedido, tokens ou payload.

Replay devolve o mesmo recibo 201/Location e indicador de replay, mesmo se o estado atual da ordem mudou. Não equivale a executar a criação novamente. Mudanças futuras do payload precisam preservar o normalizador/fingerprint v1 ou mudar namespace explicitamente. Header não autoriza recurso; identidade e membership vêm do fluxo já validado.

Retenção inicial: recibos não são removidos automaticamente. Crescimento será medido; antes de produção definir janela mínima, limpeza em lotes/quotas e semântica de chave expirada. Audit exige política de retenção/expurgo e backup operacional; append-only runtime não protege contra administrador de banco comprometido. Nenhum TTL/worker fictício.

Experimento/gates: concorrência com mesma chave e payload; chave divergente; mesma chave em tenants/atores diferentes; falha de audit impede ordem/recibo; RLS/FKs e impossibilidade de alterar audit. Benchmarks ainda inexistentes; medir sobre Small antes de alegar performance. Riscos: contenção em chave abusada, índice crescendo, pool durante disputa, custo de escrita. Limites de concorrência são contenção inicial; noisy neighbor ainda exige medição. Reconsiderar retenção/estrutura após volumes e planos reais, sem introduzir broker para uma transação local.

Referências: [isolamento Read Committed](https://www.postgresql.org/docs/18/transaction-iso.html), [checagem de unicidade concorrente](https://www.postgresql.org/docs/18/index-unique-checks.html).
