# Métricas verificadas

| Métrica | Valor | Evidência |
|---|---|---|
| SDK | 10.0.302 | `dotnet --info` |
| Runtime ASP.NET | 10.0.10 | `dotnet --info` |
| PostgreSQL | 18.6 | binário local `postgres --version` |
| Build Release | 0 warnings, 0 errors | .NET build da solução |
| Testes | 86/86 aprovados; 0 ignorados | docs/testing/reports/2026-09-17-keyset-listing.md |
| Concorrência funcional | 100 versões originais, 1 efeito confirmado | teste PostgreSQL; não é medição de RPS |
| Idempotência | 100 comandos → 1 criação/99 replays; 12 POSTs → 1 criação/11 replays | OrderCreationTests, mesma chave e efeito |
| Secret scanning | sem leaks | Gitleaks 8.30.1, working tree, defaults, redaction |
| Formatação | aprovada | dotnet format --verify-no-changes |
| Migrations | ambos modelos alinhados; upgrade local populado aprovado | EF CLI + DirectoryMigrationTests |
| Auditoria NuGet transitiva | sem vulnerabilidades reportadas pelo feed | dotnet list package --vulnerable --include-transitive; não equivale a security review |
| Usuários / RPS / percentis | não medidos | sem carga ainda |
| Nível de validação de escala | 1 em elaboração | capacity model; não empírico |

Não converter número de registros ou testes unitários em usuários suportados.
