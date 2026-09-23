# Administração de membership — núcleo transacional

2026-09-23. Entrega interna: domínio, caso de uso, persistência, migration, grants separados e testes. Nenhuma rota de escrita administrativa está publicada ou registrada na API comum. Catálogo permanece com 16 operações implementadas. Host/audiência/MFA/guard administrativos são a próxima entrega do ADR-015.

Contrato interno `ChangeMemberAccessCommand`: MemberId, Permissions, IsActive, ExpectedVersion, Key. Tenant/ator são resolvidos por host/issuer/subject/claim restritiva e reautorizados em Serializable. Flags são internas; futuro controller precisa de DTO explícito com nomes aceitos, validação de valores desconhecidos/duplicações e proteção contra mass assignment.

| Resultado | Semântica | HTTP candidato, ainda não implementado |
|---|---|---|
| Applied | alteração e audit/recibo confirmados | 200 |
| Replayed | conteúdo igual, ator autorizado, resposta original | 200 |
| Denied | contexto/alvo/permissão inválidos, sem dados do alvo | 404 uniforme |
| Invalid | chave/ID vazio, versão inválida ou bits desconhecidos | 400 Problem Details |
| Conflict | versão vencida, chave divergente, último admin ou estado igual | 409 genérico |
| Busy | orçamento de três transações esgotado | 503; repetir mesma chave |

GET `/v1/members` e `/v1/members/{id}` incluem `version` positiva, adição para edições condicionais. Paginação/autorização de leitura mantidas. `isActive` descreve o vínculo; ManageMembers não concede ReadMembers automaticamente. Migration antecede binário que lê coluna nova.

Delegação limitada impede suspender administradores com permissões superiores. Revogação preserva ordens/histórico. Se o ator retirar seu próprio ManageMembers, o replay será negado pela autorização atual; primeiro commit e recibo permanecem válidos. Retenção de audit/recibo continua pendente; não expor tabela como GET genérico. Bootstrap controlado, convites, operador global e MFA possuem backlog.

[Decisão e limites](../architecture/adr/ADR-016-membership-access-transactions.md). [Evidência](../testing/reports/2026-09-23-membership-administration-core.md).
