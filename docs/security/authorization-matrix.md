# Matriz de autorização

2026-09-22. JWT prova identidade; permissões persistidas e relação com o recurso concedem acesso. Uma persona não é uma claim confiável de role. Todos os endpoints tenant exigem domínio verificado, identidade/vínculo/tenant ativos e validação cruzada de tenant_id quando presente. Nenhuma autoridade vem de IDs no payload. Negar por padrão.

`S`: somente próprio usuário/recurso; `A`: recurso atribuído; `T`: tenant atual conforme permissão explícita; `P`: control plane separado; `—`: negado. As colunas são **perfis candidatos**, não roles implementadas nem grants automáticos. Existem sete flags de ordens, ReadMembers e ManageMembers. A última opera só no núcleo administrativo interno (ADR-016), sem rota ou grant automático. Demais permissões abaixo são propostas.

| Capability | Tenant Admin | Manager | Agent | Customer | Provider | Technician | Platform Admin |
|---|---|---|---|---|---|---|---|
| Contexto/me/permissões | S | S | S | S | S | S | — |
| Tenant settings | T | leitura | — | — | — | — | — |
| ReadMembers: lista/detalhe de vínculos | T explícito | T delegado | — | — | — | — | — |
| ManageMembers: permissões/status (núcleo interno) | T limitado às próprias flags | T se concedida explicitamente | — | — | — | — | — |
| Gerir memberships/roles/convites | T | — | — | — | — | — | — |
| Clientes: consultar/gerir | T | T | T limitado | S futuro | — | — | — |
| Prestadores: consultar/gerir | T | T | leitura | — | S limitado | S limitado | — |
| Competências/áreas/agenda | T | T | leitura | slots autorizados | S | S | — |
| Catálogo: consultar/gerir | T | T | leitura | leitura ativa | leitura ativa | leitura ativa | — |
| Solicitação: criar/consultar | T | T | T | S | — | — | — |
| Solicitação: triagem/converter | T | T | T delegado | — | — | — | — |
| ReadAllOrders / AssignOrders | T | T | T delegado | — | — | — | — |
| ReadOwnOrders / CreateOrders / CancelOwnOrders | S/T conforme flags | S/T conforme flags | S/T conforme flags | S | — | — | — |
| ExecuteAssignedOrders | A se também executor | A se também executor | — | — | A | A | — |
| ManageOrders: cancelar antes de iniciar | T | T | T delegado | — | — | — | — |
| Agendar/reagendar | T | T | T delegado | S conforme política | A conforme política | A conforme política | — |
| Comentários/anexos/timeline | T | T | T delegado | S | A | A | — |
| Notificações/preferências | S | S | S | S | S | S | — |
| Audit administrativo | T | T delegado | — | — | — | — | — |
| Busca/dashboard/reports | T | T | T delegado | somente S na busca | somente A na busca | somente A na busca | — |
| Webhooks/integrações | T com MFA | — | — | — | — | — | — |
| Subscription/usage/limits | T | leitura delegada | — | — | — | — | — |
| Provisionar/suspender tenant, publicar plano | — | — | — | — | — | — | P com MFA |

Políticas obrigatórias futuras: administrador não concede privilégio que não possui; último administrador ativo não pode ser removido/suspenso; convite é expirável, de uso único e vinculado à identidade destinatária verificada; token de convite não entra em URL/log. POST de aceite usa corpo redigido. Platform Admin não herda acesso ao tenant; suporte excepcional exige autorização explícita, prazo e auditoria.

Controllers de ordens usam a policy nomeada `tenant-access`, com as mesmas claims sub/client_id/jti/iat da fallback policy. Usar apenas `[Authorize]` sem essa policy perderia a exigência adicional das claims. Permissão de recurso continua em Application/Infrastructure, não em ifs de role no controller. GETs não exigem permission flags de escrita. IDs desconhecidos e recursos inacessíveis retornam 404 uniforme; token inválido 401; claims obrigatórias ausentes 403.

ReadMembers não é concedida por roles candidatas, flags de ordens ou migration. Retorna somente dados do vínculo local, não dados globais da conta. Runtime conserva SELECT-only. [Contrato, cursor e implantação](../api/membership-read.md).

Verificação: ApiIsolationTests, OrderCreationTests, OrderListingTests, OrderTransitionTests, CurrentContextTests, MemberReadTests e ControllerContractTests. Novos domínios precisam de cenários sem token, claims incompletas, permissão insuficiente, usuário/tenant suspenso, host/tenant_id adulterados, recurso alheio e revogação com token ainda válido. Não considerar a matriz candidata evidência de implementação.
