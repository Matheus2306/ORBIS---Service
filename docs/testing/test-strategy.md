# Estratégia de testes e Definition of Done

Domain: transições, invariantes e contexto; Application: permissões + ownership; Architecture: domínio sem dependências de EF/ASP.NET, referências direcionais; Integration: PostgreSQL real com role de runtime, RLS, FK e concorrência; Contract: HTTP status, Problem Details, paginação e idempotência; E2E: três jornadas reais após portais; Migration: upgrade, esquema e coexistência N/N+1.

Mocks não provam SQL, RLS, isolation level ou locks. Testes de banco usam cluster efêmero exclusivo, nunca base existente. Sem Docker, binários PostgreSQL locais podem iniciar cluster no workspace; em CI usar service container. Ausência da dependência falha a suíte obrigatória; não trocar para SQLite nem marcar sucesso silenciosamente.

Casos obrigatórios: A lê/altera/exclui ID de B; payload força TenantId; contexto ausente; SQL ignora filtro EF; conexão reutilizada de A por B; FK cruza memberships; token sem vínculo/inativo; permissão em A não vale em B; dono errado no mesmo tenant; tenant suspenso; host/claim conflitante; forwarded host forjado. Features futuras de arquivos/cache/search/SignalR/jobs recebem casos próprios antes de serem expostas.

Concorrência: 100 atualizações no mesmo version token, duas conclusões, dois accepts, retries com mesma chave, revogação concorrente e jobs duplicados quando houver worker. Validar número de efeitos/audits, não apenas respostas HTTP. Medir situação final no banco.

DoD: build Release, format/analyzers, testes por risco, autorização/tenant, validação/erros, observabilidade, segurança, performance, UX e banco avaliados; documentação atualizada e diff/segredos revisados. Testes funcionais verdes não aprovam release de produção.

CI planejada: restore locked → format → compile → unit/architecture → integration/contract → SAST/dependencies/secrets → migration → artifact → imagem não root → scan de imagem → performance smoke. Falta de ferramenta/ambiente deve aparecer como gate pendente ou falha, nunca badge verde equivalente. Segurança crítica e isolamento quebrado bloqueiam promoção.
