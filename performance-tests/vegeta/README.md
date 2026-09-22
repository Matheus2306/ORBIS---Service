# Gerador fixado

Este diretório contém os insumos da compilação **12.13.0-orbis.1** de Vegeta, ferramenta de teste separada do SaaS. Código original MIT obtido como módulo Go autenticado; um módulo de ferramentas seleciona as dependências corrigidas sem editar o upstream. O build-info preserva sua versão para auditoria. O bootstrap conserva LICENSE junto ao executável. Não contém tokens nem binários.

```powershell
./scripts/build-load-generator.ps1
./scripts/validate-local.ps1
```

Preparação requer Windows x64, PowerShell 7, rede para go.dev/proxy.golang.org/sum.golang.org/vuln.go.dev e espaço para SDK/cache. Nenhuma configuração global, trust store ou PATH é alterado. O SDK ZIP tem checksum fixado antes da extração em pasta nova. `GOENV=off`, `GOTOOLCHAIN=local`, `CGO_ENABLED=0`, build readonly/trimpath e fontes fixadas limitam variações; bytes diferentes são rejeitados. Locks e hashes devem ser atualizados por revisão explícita, seguida de reconstrução, auditoria e testes.

Cada execução grava `.artifacts/load-generator/<uuid>/manifest.json`, locks/licença, SDK, executável, scanner, build-info e auditoria. Fontes autenticadas permanecem no cache de módulos local. Somente sucesso atualiza `current.json`. Uma interrupção pode deixar pasta parcial; sem manifesto aprovado ela não é utilizada. Nenhuma pasta é apagada automaticamente. O gate revalida hashes do binário/scanner e dos três insumos versionados, e consulta novamente advisories dos módulos presentes no binário. O conjunto completo de módulos upstream de desenvolvimento não é certificado por essa análise do executável.

Os sete testes `LoadGeneratorTests` são transporte/contrato: seis requisições independentes e duas criações com a mesma chave para conferir replay. Tokens entram por stdin; resultados não capturam corpos e são verificados contra vazamento do token usado. Porta dinâmica/loopback e CA efêmera, proxies removidos, redirects proibidos. Exit code do Vegeta não comprova status HTTP/TLS: cada amostra é interpretada.

Ainda não existe cenário de baseline. Um benchmark válido exige host separado, Small, mistura explícita de operações, ≥5 min, percentis por endpoint, métricas de todos os processos e taxa oferecida/alcançada. Não usar os testes unitários de transporte para reportar RPS ou usuários suportados. Veja [ADR-013](../../docs/architecture/adr/ADR-013-load-generator.md).
