# Próximas ações

## Prioridade funcional solicitada em 2026-09-22

Contexto atual, controllers e consulta de memberships concluídos, 175 testes e 12 checks aprovados (relatório 2026-09-23-membership-read). Ler `api/api-gap-analysis.md`, `api/domain-capability-matrix.md`, `api/endpoint-catalog.md` e `security/authorization-matrix.md`. Catálogo: 111 operações, 16 implementadas; 41 P0/42 P1 restantes.

1. Modelar fronteira administrativa: identidade do operador, delegação limitada, último administrador, audit transacional, expectedVersion e recibos. Decidir processo/grants separados antes de comandos; não ampliar runtime comum para mutar directory/memberships nem confiar em role declarada no token.
2. Implementar provisionamento/convites/memberships a partir dessa fronteira. Resolver IdP e inicialização local reportada pelo usuário; token de convite expirável, uso único, destinatário verificado, sem token em URL/log. ReadMembers não concede escrita nem surge automaticamente em migrations.
3. Clientes → prestadores → catálogo → solicitações, um domínio por incremento, atualizando matriz/catálogo/testes/estado/commit.
4. Workflow/agenda/anexos/notificações/auditoria conforme dependências; não criar rotas vazias.
5. Manter os gates abaixo; testes funcionais não provam ausência de regressão de performance MVC.

## Gates técnicos pendentes

1. Implementar host Performance separado do gerador e coletar recursos de API/DB/gerador. Vegeta fixado já validado em HTTPS, 126 testes e 26 regras verdes. Preservar JWT real, host/membership, RLS, VerifyFull e limiters; declarar discovery de teste como exclusão. Windows x64 é a única plataforma do bootstrap validada.
2. Medir baseline Release Small (warmup + ≥5 min), mix explícito de operações e credenciais válidas durante toda a janela. Writes precisam de chaves únicas para não medir só replays. Comparar taxa oferecida/alcançada, timestamps e saturação do gerador; reportar percentis por endpoint, sem extrapolar usuários.
3. Investigar planos de escrita e custos de transação/índices se o baseline mostrar gargalo; não converter tempos SQL isolados em capacidade de API.
4. Levar o gate local para CI remoto, acrescentar imagem/scans/performance smoke e grants operacionais. Não declarar CI verde por execução local.
5. Definir quotas/retenção, integração IdP e portais; evolução de carga depende de evidência anterior.

Depois: integração de identidade, UX, anexos, jobs, operacionalização, expansão de carga. Cada etapa referencia os gates de backlog; não publicar antes do readiness review.
