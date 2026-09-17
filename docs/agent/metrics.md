# Métricas verificadas

| Métrica | Valor | Evidência |
|---|---|---|
| SDK | 10.0.302 | `dotnet --info` |
| Runtime ASP.NET | 10.0.10 | `dotnet --info` |
| PostgreSQL | 18.6 | binário local `postgres --version` |
| Build Release | 0 warnings, 0 errors | .NET build da solução |
| Testes iniciais | 13/13 aprovados; 0 ignorados | .artifacts/test-results/foundation.trx |
| Formatação | aprovada | dotnet format --verify-no-changes |
| Auditoria NuGet transitiva | sem vulnerabilidades reportadas pelo feed | dotnet list package --vulnerable --include-transitive; não equivale a security review |
| Usuários / RPS / percentis | não medidos | sem carga ainda |
| Nível de validação de escala | 1 em elaboração | capacity model; não empírico |

Não converter número de registros ou testes unitários em usuários suportados.
