# Próximas ações

## Prioridade funcional solicitada em 2026-09-22

Contexto, ordens, consulta de memberships e comandos administrativos concluídos; 262 testes e 12 checks aprovados (relatório2026-09-24-administrative-http). Ler catálogo/matriz/autorização e ADRs015/016/017. Catálogo:111 operações,19 implementadas;38 P0/42 P1 restantes. Publicações locais separadas verificadas; não há deployment nem IdP operacional.

1. Definir domínio/contrato de convite e aceite: destinatário com identidade verificada pelo IdP, token aleatório expirável/de uso único, armazenamento do hash, delegação limitada, quotas, audit e revogação. Resolver como comprovar destinatário sem confiar em e-mail declarado no payload. Implementar um fluxo completo, com RLS/constraints, concorrência e testes HTTP/negativos; sem conceder grants de escrita à API comum.
2. Integrar IdP/cliente administrativo e bootstrap operacional para execução persistente. Configuração do host em operations/administration-host.md; validar emissão real de acr/auth_time, rotação, domínios verificados, certificados e primeira membership. Operador global exige relação própria antes de provisionamento. Nenhuma role JWT concede acesso global.
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
