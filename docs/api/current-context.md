# Contexto atual — Identity, Tenancy e Authorization

GET `/v1/me` → `{ userId, tenantId }`.

GET `/v1/tenant` → `{ id, name }` da organização atual.

GET `/v1/me/permissions` → `{ tenantId, userId, permissions: ["ReadOwnOrders", "CreateOrders"] }`; nomes são parte do contrato público. Novas permissões podem ser acrescentadas; clientes devem ignorar nomes desconhecidos. Renomear/remover uma permissão requer análise de compatibilidade. A lista não concede autoridade para comandos futuros.

Essas consultas atendem ao bootstrap dos portais, são classificadas como Normal Path e têm orçamento nominal p95≤300ms/p99≤800ms, pico p95≤1000ms/p99≤2000ms. Metas ainda sem benchmark HTTP; não declarar validação de latência. Respostas não incluem claims brutas, issuer/subject, contatos, papéis inventados ou lista de organizações.

Autorização: policy tenant-access (JWT/claims), domínio verificado, identidade/tenant ativos e membership ativo no tenant. Permissão de ordem não é exigida para ler o próprio contexto: um membro ativo sem flags recebe contexto e array vazio. Não recebe permissão implícita para listar ordens ou administrar outros usuários. TenantId/actorId em query/headers não selecionam contexto; claim tenant_id divergente nega acesso. GETs são sem efeitos, sem idempotency-key nem auditoria de mutação; logs não devem conter payload de identidade.

200 JSON com Cache-Control no-store; 401 token ausente/inválido; 403 claims obrigatórias ausentes; 404 uniforme para host não verificado, identidade/vínculo ausente, suspensão ou tenant_id divergente. 429 limite compartilhado database-operations; 503 dependência indisponível e500 erro inesperado. OpenAPI documenta os três DTOs em Development. Sem `/me/memberships` global: depende de política explícita para vínculos entre organizações, sem bypass de RLS.

Persistência: resolução parametrizada do diretório; uma consulta de membership por chave composta dentro de transação com contexto RLS; somente depois consulta do nome por PK/tenant ativo. Três SELECTs de dados por leitura bem-sucedida, sem coleções ilimitadas/N+1. A transação de membership é encerrada antes da segunda conexão; teste com pool máximo1 comprova que o fluxo não depende de reservar duas conexões simultâneas. Não introduz migration, grants ou cache. Revogação observada a cada novo request; operações já autorizadas têm a semântica Read Committed do ADR-004, sem cancelamento retroativo.

Testes em CurrentContextTests e ControllerContractTests: projeções mínimas, token/claims, multi-membership com permissões diferentes, spoof/tenant errado, revogação de usuário/vínculo/tenant, usuário desconhecido, permissão vazia, alteração de permissão com token válido e pool liberado. Relatório do incremento registra o resultado; teste de funcionalidade não é benchmark de capacidade.
