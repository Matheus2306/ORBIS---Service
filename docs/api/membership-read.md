# Consulta de memberships

2026-09-23. Incremento P0 de leitura, separado de convites/administração ainda não implementados. Um vínculo é identificado pelo par tenant/UserId; `{id}` é o UserId dentro do tenant resolvido pelo host, não ID de um perfil de cliente/prestador.

| Operação | Resposta | Regra |
|---|---|---|
| GET `/v1/members?limit=25&status=all` | MemberPage: items, nextCursor | máximo 100, status all/active/suspended |
| GET `/v1/members/{id}` | MemberDetails: userId, isActive, permissions | apenas vínculo no tenant atual |

As duas operações exigem `ReadMembers` (128) persistida em membership ativa, além de JWT válido, claims obrigatórias, tenant/usuário ativos e domínio verificado. Flags de ordens, inclusive ManageOrders, não concedem acesso. Nenhum perfil recebe a nova flag automaticamente; não existe endpoint de concessão neste incremento. `/me` continua disponível para leitura do próprio contexto sem ReadMembers. `isActive` descreve o vínculo, não substitui o status global da conta.

Respostas de sucesso usam `Cache-Control: no-store`; DTOs não incluem email, documento, identidade externa ou outros tenants. Sem permissão/recurso/tenant: 404 uniforme. Autenticação: 401; claims incompletas: 403. Parâmetros inválidos/duplicados: 400 Problem Details sem eco do input. Limiter compartilhado por instância pode devolver 429; dependência indisponível 503. Métricas HTTP nativas usam templates de rota. Sem auditoria de leitura persistida neste incremento; consultas não alteram estado.

## Paginação e custo

Ordenação crescente por user_id, desempate dispensável porque a PK é `(tenant_id,user_id)`. Cursor v1 Base64Url, no máximo 512 caracteres, vinculado a tenant/ator/status; vazio, versão/IDs/filtro inválidos são rejeitados. Cursor é posição não confiável, sem assinatura e sem autoridade: cada página resolve o contexto e reautoriza no banco. Alterar a posição pode pular linhas autorizadas, nunca ampliar escopo. Mudança de filtro exige começar nova paginação. Não oferece snapshot de múltiplos requests; mudanças concorrentes de status/membership podem mudar páginas seguintes.

Por request bem-sucedido: uma resolução limitada de diretório, uma verificação de permissão e uma consulta de membership; uma conexão por vez. Query tenant-scoped, AsNoTracking, projeção mínima, `limit+1`, sem COUNT, OFFSET ou N+1. RLS forçada complementa o filtro EF. Seek traduzido pelo provider para comparação UUID, não CASE/CompareTo. A PK existente é candidata ao plano; filtro de status pode examinar linhas adicionais. Índice de status, cache e outro motor de busca não foram adicionados sem gargalo medido.

Meta nominal p95≤300ms/p99≤800ms; pico p95≤1000ms/p99≤2000ms. Teste do formato SQL/resultado não comprova plano eficiente nem SLO: baseline HTTP, plano em hot tenant e ensaios de carga continuam pendentes.

## Banco, implantação e rollback

`ReadMemberPermission` expande a constraint de 0..127 para 0..255, sem conceder bits, alterar grants, RLS, PK ou dados. Novos bits desconhecidos continuam rejeitados por domínio e banco. Runtime continua SELECT-only em memberships; DDL usa credencial operacional separada, nunca a API.

Aplicar migration antes dos binários que provisionarem a flag. Versão anterior da API mantém flags de ordens e ignora ReadMembers na projeção de nomes; não ganha a nova rota. Preferir rollback de binário preservando schema expandido. Down do schema falha e reverte atomicamente se existir qualquer bit128: exige decisão explícita/auditada de revogação, não limpa flags automaticamente.

Decisão atual: troca transacional da CHECK validada, `lock_timeout=3s`, `statement_timeout=10s`. A espera e o scan não podem bloquear indefinidamente. A alteração requer lock exclusivo e scan; não é alegação de zero downtime. Teste local usa Small (1.050 vínculos, 1.000 usuários, 50 tenants, 10.000 ordens), preserva hash do conteúdo, simula lock impeditivo, valida unknown bits e rollback recusado. Medição no relatório do incremento. Para bases maiores, executar rehearsal em cópia representativa antes de promover. Se o orçamento exceder, redesenhar expansão `NOT VALID`/validação em transações independentes com testes de reexecução; não aumentar timeouts indiscriminadamente.

Alternativas: reutilizar ReadAllOrders foi rejeitado por confundir administração de membros com operação de serviços; offset e retorno de contas globais foram rejeitados por custo/escopo. Normalizar todas as permissões em tabelas neste incremento não resolve uma necessidade já medida; reavaliar ao implementar papéis customizáveis. Os nomes das permissões retornados são contrato público e não devem ser renomeados junto com refactors internos.

Referências primárias: [Npgsql: row comparisons](https://www.npgsql.org/efcore/mapping/translations.html#row-value-comparisons), [PostgreSQL: ALTER TABLE e validação de constraints](https://www.postgresql.org/docs/current/sql-altertable.html). Contratos/testes: MembersController, MemberReadTests, MemberCursorTests e DatasetTests. Administração permanece bloqueada até boundary privilegiada, auditoria, proteção do último administrador, idempotência e concorrência próprias.
