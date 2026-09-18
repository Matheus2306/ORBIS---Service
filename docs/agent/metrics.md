# Métricas verificadas

| Métrica | Valor | Evidência |
|---|---|---|
| SDK | 10.0.302 | `dotnet --info` |
| Runtime ASP.NET | 10.0.10 | `dotnet --info` |
| PostgreSQL | 18.6 | binário local `postgres --version` |
| Build Release | 0 warnings, 0 errors | .NET build da solução |
| Testes | 114/114 aprovados; 0 ignorados | docs/testing/reports/2026-09-18-database-tls.md |
| Transporte PostgreSQL | TLS obrigatório + VerifyFull; plaintext/CA alheia/nome errado negados | DatabaseTransportTests; sem medição de overhead |
| Dataset persistido | Small, 1.000 usuários / 50 tenants / 10.000 ordens, Uniform e HotTenant | manifesto; hashes iguais em bancos independentes, sem validação de carga |
| Planos SQL | 16 consultas × 30 amostras × 2 perfis, RLS ativa | docs/performance/reports/2026-09-18-small-query-plans.md; não mede HTTP/RPS |
| Concorrência funcional | 100 versões originais, 1 efeito confirmado | teste PostgreSQL; não é medição de RPS |
| Idempotência | 100 comandos → 1 criação/99 replays; 12 POSTs → 1 criação/11 replays | OrderCreationTests, mesma chave e efeito |
| Conclusão concorrente | 100 mesmas chaves → 1 efeito/99 replays; 100 chaves distintas → 1 efeito/99 conflitos | OrderTransitionTests; não é benchmark |
| Secret scanning | sem leaks | Gitleaks 8.30.1, working tree, defaults, redaction |
| Formatação | aprovada | dotnet format --verify-no-changes |
| Migrations | ambos modelos alinhados; upgrade local populado aprovado | EF CLI + DirectoryMigrationTests |
| Auditoria NuGet transitiva | sem vulnerabilidades reportadas pelo feed | dotnet list package --vulnerable --include-transitive; não equivale a security review |
| Usuários ativos / RPS / percentis HTTP | não medidos | sem carga HTTP ainda |
| Nível de validação de escala | 1 em elaboração | capacity model; não empírico |

Não converter número de registros ou testes unitários em usuários suportados.
