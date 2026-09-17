# ADR-004 — identidade global e memberships

Status: arquitetura decidida; provedor OIDC de produção não escolhido. Problema: uma pessoa participa de várias organizações com papéis distintos. Requisitos: MFA administrativo, revogação, recuperação, sessões seguras, API independente.

Alternativas: User.TenantId não atende múltiplas relações; login/senhas próprios via Identity exigem operação de credenciais; OIDC externo delega autenticação, mantendo autorização no produto. Decisão: contrato OIDC/OAuth2, identidade externa por issuer+subject mapeada a usuário global. Membership (tenant,user) tem status e permissões; papéis são conjuntos de permissões, não ifs por nome de role. Acesso ao recurso exige também ownership/designação. Claims de tenant não substituem leitura de vínculo ativo. Revogação tem efeito no próximo request autorizado; operações em andamento possuem semântica transacional documentada.

Web: avaliar BFF com cookie HttpOnly/Secure/SameSite e antiforgery para mutações; API valida assinatura/issuer/audience/lifetime. Não guardar tokens em localStorage. Callback possui state/nonce/PKCE e redirect URI exato. Sem endpoint de token caseiro. Desenvolvimento/testes não podem habilitar bypass em Production.

Benchmarks: inexistentes. Riscos: dependência externa, custos por MAU, sequestro de token, tenant confusion e cache de permissão obsoleto. Não selecionar fornecedor sem custos e requisitos de residência/MFA. Reconsiderar ASP.NET Identity hospedado se restrições contratuais/custo justificarem responsabilidade operacional. Produção bloqueada até fluxo real e revogação serem testados.
