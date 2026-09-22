# Próximas ações

## Prioridade funcional solicitada em 2026-09-22

Controllers/auditoria concluídos, 140 testes e 12 checks aprovados. Ler `api/api-gap-analysis.md`, `api/domain-capability-matrix.md`, `api/endpoint-catalog.md` e `security/authorization-matrix.md`. Catálogo: 111 operações, 11 implementadas; 46 P0/42 P1 restantes.

1. Implementar GET `/v1/me`, `/v1/me/permissions` e `/v1/tenant` para vínculo ativo no contexto atual; reutilizar resolução/RLS e testar negações/revogação, sem enumerar tenants alheios ou expor claims brutas.
2. Provisionamento/convites/memberships com control plane e grants separados; não ampliar runtime comum para mutar directory/memberships. Resolver IdP e inicialização local reportada pelo usuário.
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
