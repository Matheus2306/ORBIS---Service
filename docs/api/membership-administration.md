# Administração de membership — API separada

2026-09-24. Três rotas implementadas somente em `Orbis.Administration.Api`, com credencial/audiência/MFA próprias. A API comum continua com16 operações; catálogo combinado possui19 operações únicas. [Configuração para executar](../operations/administration-host.md) e [ADR-017](../architecture/adr/ADR-017-administrative-http-boundary.md). Nenhum serviço foi publicado em produção.

Contrato interno `ChangeMemberAccessCommand`: MemberId, Permissions?, IsActive?, ExpectedVersion, Key. Campo ausente é preservado dentro da transação, sem read-modify-write fora dela. Tenant/ator são resolvidos por host/issuer/subject/claim restritiva e reautorizados em Serializable. Flags internas são expostas somente por nomes exatos conhecidos, sem números, duplicações ou campos extras.

| Operação administrativa | Body JSON estrito |
|---|---|
| PATCH `/v1/members/{id}/permissions` | `{"permissionSet":["ReadOwnOrders","ReadMembers"],"expectedVersion":1}` |
| POST `/v1/members/{id}/suspend` | `{"expectedVersion":1}` |
| POST `/v1/members/{id}/activate` | `{"expectedVersion":2}` |

Todas exigem HTTPS, Bearer administrativo, `Idempotency-Key` UUID não vazio e ManageMembers persistida. `permissionSet:[]` remove todas as flags se delegação/último admin permitirem. Body até8KiB, camelCase exato, propriedades duplicadas/desconhecidas rejeitadas. `reason` da proposta anterior não foi incorporado: texto livre requer definição própria de retenção/redação. Resposta200: userId, isActive, permissions (nomes), version; header Idempotency-Replayed indica replay. Cache-Control no-store inclui falhas.

| Resultado | Semântica | HTTP implementado |
|---|---|---|
| Applied | alteração e audit/recibo confirmados | 200 |
| Replayed | conteúdo igual, ator autorizado, resposta original | 200 |
| Denied | contexto/alvo/permissão inválidos, sem dados do alvo | 404 uniforme |
| Invalid | chave/ID vazio, versão inválida ou bits desconhecidos | 400 Problem Details |
| Conflict | versão vencida, chave divergente, último admin ou estado igual | 409 genérico |
| Busy | orçamento de três transações esgotado | 503; repetir mesma chave |

GET `/v1/members` e `/v1/members/{id}` incluem `version` positiva, adição para edições condicionais. Paginação/autorização de leitura mantidas. `isActive` descreve o vínculo; ManageMembers não concede ReadMembers automaticamente. Migration antecede binário que lê coluna nova.

Delegação limitada impede suspender administradores com permissões superiores. Revogação preserva ordens/histórico. Se o ator retirar seu próprio ManageMembers, o replay será negado pela autorização atual; primeiro commit e recibo permanecem válidos. Retenção de audit/recibo continua pendente; não expor tabela como GET genérico. Bootstrap controlado, convites, operador global e integração real de MFA/IdP possuem backlog.

401 token ausente/inválido;403 contrato assinado de MFA/client/claims não satisfeito;404 uniforme para alvo/contexto/permissão;409 versão, chave, estado igual ou último admin;429 limite local4 sem fila;503 dependência, contenção ou orçamento cooperativo5s. Após falha ambígua, repetir mesma chave/body; autorização atual é rechecada mesmo no replay. Conflitos exigem ler versão atual pela API comum e decidir nova intenção, sem sobrescrever automaticamente.

[Decisão e limites](../architecture/adr/ADR-016-membership-access-transactions.md). [Evidência](../testing/reports/2026-09-23-membership-administration-core.md).
