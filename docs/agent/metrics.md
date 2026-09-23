# Métricas verificadas

| Métrica | Valor | Evidência |
|---|---|---|
| SDK | 10.0.302 | `dotnet --info` |
| Runtime ASP.NET | 10.0.10 | `dotnet --info` |
| PostgreSQL | 18.6 | binário local `postgres --version` |
| Build Release | 0 warnings, 0 errors | .NET build da solução |
| Testes | 185/185 aprovados; 0 ignorados | docs/testing/reports/2026-09-23-runtime-privilege-boundary.md |
| Gerador externo | Vegeta 12.13.0-orbis.1; build idêntico/auditado, 7 casos de transporte | 8 requests funcionais; nenhum baseline/RPS validado |
| Métricas nativas | HTTP/TLS/conexões/pool emitidos; atributos controlados | NativeMetricsExposeBoundedRoutesAndPoolWithoutRequestData; sem baseline nem coletor externo |
| Transporte HTTPS | HTTP/1.1 e HTTP/2 reais, CA alheia/nome errado negados | Kestrel em Performance; autenticação/isolamento/replay mantidos, sem carga |
| Transporte PostgreSQL | TLS obrigatório + VerifyFull; plaintext/CA alheia/nome errado negados | DatabaseTransportTests; sem medição de overhead |
| Dataset persistido | Small, 1.000 usuários / 50 tenants / 10.000 ordens, Uniform e HotTenant | manifesto; hashes iguais em bancos independentes, sem validação de carga |
| Planos SQL | 16 consultas × 30 amostras × 2 perfis, RLS ativa | docs/performance/reports/2026-09-18-small-query-plans.md; não mede HTTP/RPS |
| Concorrência funcional | 100 versões originais, 1 efeito confirmado | teste PostgreSQL; não é medição de RPS |
| Idempotência | 100 comandos → 1 criação/99 replays; 12 POSTs → 1 criação/11 replays | OrderCreationTests, mesma chave e efeito |
| Conclusão concorrente | 100 mesmas chaves → 1 efeito/99 replays; 100 chaves distintas → 1 efeito/99 conflitos | OrderTransitionTests; não é benchmark |
| Secret scanning | sem leaks | Gitleaks 8.30.1, working tree, defaults, redaction |
| Formatação | aprovada | dotnet format --verify-no-changes |
| Gate local | 12 checks aprovados, 185 testes + 26 casos das regras | docs/testing/reports/2026-09-23-runtime-privilege-boundary.md; CI remoto não executado |
| Privilégios do runtime | grant por coluna e SET ROLE transitivo antes aceitos, agora startup rejeitado | dois regressivos reproduzidos; 15 testes focados; política sem memberships/GRANT OPTION, matriz de 9 tabelas |
| API | 111 operações catalogadas; 16 implementadas; 13 de negócio em controllers | 31 domínios planejados; nenhum baseline de domínio completo; sem carga HTTP |
| Migration ReadMembers | Small: 27,808 ms, uma amostra no gate; conteúdo preservado; lock/Down recusados atomicamente | relatório 2026-09-23-membership-read; não mede API nem volumes maiores |
| Pools de dataset | 2→0 e 8→0 sessões por teste; max_connections=40 preservado | observação pg_stat_activity; evita acúmulo entre bancos descartáveis |
| Migrations | ambos modelos alinhados; upgrade local populado aprovado | EF CLI + DirectoryMigrationTests |
| Auditoria NuGet transitiva | sem vulnerabilidades reportadas pelo feed em 2026-09-23 | oito projetos; não equivale a security review |
| Auditoria Go | sem vulnerabilidades reportadas nos módulos do gerador final | govulncheck 1.8.0 / module / vuln.go.dev, 2026-09-23; não certifica código de desenvolvimento fora do binário |
| Usuários ativos / RPS / percentis HTTP | não medidos | sem carga HTTP ainda |
| Nível de validação de escala | 1 em elaboração | capacity model; não empírico |

Não converter número de registros ou testes unitários em usuários suportados.
