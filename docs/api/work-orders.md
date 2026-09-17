# API de ordens — contrato implementado

`GET /v1/work-orders/{id:guid}` exige access token JWT e Host de domínio verificado no diretório. Resposta 200: id, description, status (nome textual do estado), version, createdAt UTC. `Cache-Control: no-store`. Nenhum dado de outro usuário/tenant integra o DTO. Lista e transições ainda não estão expostas.

401: token ausente/inválido, assinatura/tipo/issuer/audience/lifetime incorretos; WWW-Authenticate Bearer sem detalhes internos. 403: principal autenticado não cumpre policy de claims. 404 uniforme: recurso ausente, host não registrado/não verificado, tenant/usuário/membership inativo, vínculo ou permissão insuficiente, tenant_id assinado divergente/inválido. 429: 16 operações simultâneas por instância, compartilhadas entre GET/POST, sem fila. 503: indisponibilidade/timeout PostgreSQL direto; erro inesperado de persistência retorna 500. Erros usam Problem Details com traceId; não retornam SQL/credenciais. Nenhuma promessa de ausência de side-channel temporal foi medida.

Host é seletor, nunca prova de acesso. X-Tenant-Id e X-Forwarded-Host não são fontes de autoridade. Claim tenant_id opcional somente restringe o domínio resolvido; token de usuário multi-tenant mantém permissões distintas por vínculo. Membership/estado global consultados a cada acesso. TLS HTTP/ingress confiável devem ser configurados e testados antes de exposição pública.

## Configuração externa obrigatória

- `Authentication__Authority`: issuer HTTPS exato, também origem de discovery; sem credenciais/query/fragmento.
- `Authentication__Audience`: audiência exclusiva da API.
- `ConnectionStrings__Orbis`: segredo da role runtime restrita; nunca role de migration. Pool máximo imposto 20, timeout de conexão/comando 5s. Fora Development/Testing exige `SSL Mode=VerifyFull` e confiança no certificado PostgreSQL.
- `ASPNETCORE_ENVIRONMENT`: Development, Testing, Performance, Staging ou Production. Não usar Testing para uma implantação pública.

JWT exige at+jwt, RS256/PS256/ES256 e sub/client_id/jti/iat; clock skew 30s. Autoridade deve emitir access tokens compatíveis. Não existe emissão local de tokens no produto. ApiFactory usa discovery estático SOMENTE no processo de teste, com assinatura real e chave efêmera.

Runtime lê directory e memberships e pode SELECT/INSERT/UPDATE work_orders. Audit/recibos permitem somente SELECT/INSERT. Não pode DDL, ownership herdado, BYPASSRLS, SUPERUSER, TRUNCATE ou mutar memberships/directory. Grants concretos estão no fixture; provisionamento operacional reproduzível ainda é gate. Startup verifica flags e privilégios mínimos; não certifica conteúdo das policies. /health/live não acessa banco; /health/ready verifica banco, reutiliza resultado no máximo 5s e limita 4 requests concorrentes. Remoção de instância não saudável depende do futuro balanceador.

## Criar solicitação

`POST /v1/work-orders`, JSON `{ "description": "Descrever o serviço necessário" }`. Header `Idempotency-Key` obrigatório: UUID não vazio no formato com hífens. Permissão CreateOrders do vínculo ativo; tenant e cliente derivados da identidade validada. Campos extras, incluindo tenantId/customerId/status, são rejeitados com 400. Descrição 1–2000 caracteres, sem NUL; espaços externos removidos.

201 com `{ id, createdAt }`, Location relativa e `Idempotency-Replayed: false`. Repetir a mesma chave/ator/tenant com a mesma descrição normalizada retorna o mesmo recibo 201 e header true; conteúdo diferente retorna 409. Replay também revalida autorização. Id/data originais são estáveis, independentemente de alterações posteriores da ordem; consulte GET para estado atual. Um timeout não permite inferir ausência de commit: repetir com a MESMA chave. Nova chave significa nova intenção de criação.

Ordem, recibo e audit transacionam juntos; falha de qualquer escrita desfaz o conjunto. Recibos não expiram automaticamente nesta versão. Retenção/limpeza/quotas antes de produção conforme ADR-011. Não há cobrança, evento externo ou broker no comando.
