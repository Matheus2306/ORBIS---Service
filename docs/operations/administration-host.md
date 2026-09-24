# Executar o host administrativo

2026-09-24. Processo `src/Orbis.Administration.Api`, separado de `src/Orbis.Api`. Não fornece tela de login, conta inicial nem token de demonstração. O IdP precisa emitir JWT de acesso com MFA verificada conforme ADR-017. Testes efêmeros não configuram um ambiente persistente.

## Configuração externa obrigatória

| Variável de ambiente | Valor/contrato |
|---|---|
| ASPNETCORE_ENVIRONMENT | Development, Testing, Performance, Staging ou Production |
| ConnectionStrings__OrbisAdministration | conexão vinda de cofre/ambiente local; role dedicada `orbis_membership_admin`; VerifyFull e raiz confiável fora de Development/Testing |
| Administration__Authentication__Authority | issuer/discovery HTTPS exato do IdP |
| Administration__Authentication__Audience | audiência exclusiva do recurso administrativo |
| Administration__CommonApiAudience | audiência real da API comum; deve ser diferente |
| Administration__RequiredAcr | valor exato que o provedor garante somente após MFA |
| Administration__AllowedClientIds__0 | client_id de cliente administrativo autorizado; até16 valores indexados |
| Administration__MaximumAuthenticationAgeSeconds | opcional, default900; intervalo60–900 |
| Kestrel__Endpoints__Https__Url | listener HTTPS e porta administrativa |
| Kestrel__Certificates__Default__Path | certificado do listener obtido da operação local/cofre |
| Kestrel__Certificates__Default__Password | se exigida pelo certificado, somente via armazenamento de segredo |

O provedor deve produzir `at+jwt`, assinatura aceita RS256/PS256/ES256, issuer exato, exp, sub/client_id/jti/iat, uma única aud administrativa, acr e auth_time. O relógio precisa estar sincronizado. Refresh não altera auth_time. Configurar o fluxo do cliente para nova autenticação diante de403 por MFA; o servidor não redireciona para login.

Antes de iniciar, aplicar migrations existentes com credencial de migration fora dos processos; criar role específica sem superuser/BYPASSRLS/CREATEDB/CREATEROLE/REPLICATION ou memberships; aplicar `infrastructure/postgresql/membership-administration-grants.sql`. Essa role não recebe os grants de `orbis_runtime`. Startup valida matriz de privilégios e RLS; falha é motivo para corrigir grants, não desativar o guard.

Domínio deve existir e estar verificado em directory.tenant_domains, tenant/usuário/membership ativos, identidade externa com issuer+sub e ManageMembers concedida por operação controlada. Não dar essa flag a todo usuário. Credencial de teste/owner nunca é conexão do host.

```powershell
dotnet run --project src/Orbis.Administration.Api -c Release --no-launch-profile
```

Configurar HTTPS antes desse comando. Common e admin podem usar o mesmo host verificado com portas locais distintas; em produção, alias administrativo verificado por tenant/roteamento separado. ForwardedHeaders não estão habilitados: proxy terminando TLS e encaminhando HTTP será rejeitado nas rotas de negócio. OpenAPI `/openapi/v1.json` somente Development; `/health/live` e `/health/ready` não retornam dados de tenants. Restringir probes à rede operacional na implantação.

Monitorar série de host separada, pool `orbis-membership-administration`, 429/503, duração por rota, retries e resultados do Meter Orbis.MembershipAdministration. Counters não registram payload/token/IDs. Budget de conexões =20×instâncias comuns +8×administrativas +reserva operacional; pool cap não é demanda medida. Cliente repete UUID original após503/perda de resposta, nunca nova chave às cegas.

Reprodução funcional/segurança: `scripts/validate-local.ps1` inclui a suíte nova automaticamente. Build de distribuição: `dotnet publish src/Orbis.Administration.Api -c Release --no-restore`. Deployment, container scan, DNS, IdP real, backup/restore e carga continuam gates pendentes. Rollback: retirar tráfego e reverter binário/configuração administrativa; preservar schema/histórico/recibos e manter API comum com sua credencial original.
