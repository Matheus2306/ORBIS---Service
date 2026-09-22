# ADR-013 — gerador HTTP externo com transporte verificado

Data: 2026-09-22. Estado: adotado para validação local Windows x64; [gate e transporte aprovados](../../testing/reports/2026-09-22-load-generator.md). Capacidade de carga ainda não medida.

## Problema, contexto e requisitos

F5 precisa oferecer uma taxa de chegadas explícita ao Kestrel Release, com JWT, hostname do tenant, HTTPS validado e PostgreSQL VerifyFull. O host disponível é Windows x64, sem Docker/k6/distribuição WSL; Java encontrado é antigo/32 bits. Não instalar confiança global nem usar `insecure`. Autotestes HTTP existentes não medem carga. O gerador precisa registrar amostras, erros, taxa oferecida/alcançada e seus próprios recursos, separado do processo da aplicação.

## Alternativas

| Opção | Adequação e custo |
|---|---|
| .NET HttpClient próprio | Transporte já disponível; exigiria construir e validar scheduler, amostras, percentis e controle de saturação. Não justificado para a primeira carga. |
| PostgreSQL / QueryProbe | Útil para planos SQL; não exercita transporte/JWT/serialização e não substitui carga HTTP. |
| k6 | Bom candidato para cenários abertos e jornadas. Não instalado/experimentado; configuração de confiança privada por processo no Windows ainda não demonstrada neste projeto. Não declarar limitação universal do produto. |
| NBomber | Integração C# atraente; condições comerciais para uso organizacional precisam ser resolvidas antes da adoção. Nenhuma licença contratada. |
| Locust | Jornadas em Python; modelo de usuários/pacing precisa ser distinguido de uma taxa externa de chegadas. Introduz ambiente Python para este fim. |
| JMeter / Gatling | Ferramentas maduras; requerem runtime adicional suportado. Gatling documenta confiança permissiva por padrão, que precisaria ser substituída por truststore verificado. Nenhum experimento local. |
| Vegeta | CLI MIT, taxa de chegada, amostras, CA explícita e mapeamento de conexão preservando hostname. Adequado a requisições independentes; jornadas dependentes e distribuição exigem orquestração adicional. |

## Experimento e evidência

O binário oficial Vegeta 12.13.0, com hash conferido, informa Go 1.22.12. A política Go mantém somente duas linhas principais: esse runtime está fora de suporte. O scanner oficial govulncheck 1.8.0 reportou 63 achados de símbolos nesse binário. Isso é análise estática, não exploração de 63 falhas neste ambiente.

Recompilar o mesmo código com Go 1.27.1 deixou dois achados de símbolos em x/net. Atualizar somente para x/net 0.55.0 removeu esses dois, mas restaram avisos de pacote/módulo não chamados. O grafo final fixa x/net 0.56.0 e x/text 0.39.0, suas dependências selecionadas, e diretiva Go 1.27.1. A análise do binário resultante não reportou vulnerabilidades. Não houve alteração de código upstream. `go.mod`, `go.sum`, versão/soma do módulo, SDK/arquivo SHA256 e hash do executável estão versionados em `performance-tests/vegeta`.

A compilação final usa um módulo de ferramentas separado que requer o pacote upstream versionado e seleciona as duas dependências corrigidas. Isso preserva `vegeta/v12 v12.13.0` no build-info; copiar/editar o módulo principal o identificava como `(devel)`, limitando a análise de advisories do próprio Vegeta. Esse candidato anterior não será promovido. Arquivos de dependências divergentes do manifesto invalidam o binário, mesmo que ele tenha sido aprovado anteriormente.

O bootstrap reconstrói em diretório novo, exige binário e scanner idênticos aos hashes fixados, verifica módulos e executa nova auditoria no nível **module**, mais amplo que símbolos chamados. Falhas preservam manifesto e não promovem o candidato. O teste externo comprovou 200 autorizado, 404 recurso/host de outro tenant, 401 assinatura inválida, rejeições x509 de CA/nome e criação/replay idempotentes. Nenhum desses requests é benchmark.

## Decisão e implementação

Adotar Vegeta **12.13.0-orbis.1**, uma compilação local identificada, somente para ferramentas de validação. A API continua .NET/PostgreSQL sem dependência Go. Preparação explícita por `scripts/build-load-generator.ps1`; validação não baixa executáveis automaticamente. O gate revalida hash e advisories. Não usar o binário oficial antigo como fallback.

CA restrita ao processo, `connect-to` para loopback, proxies removidos do filho, sem redirects, captura de corpo desativada e tokens por stdin. Os testes usam no máximo um worker/request por execução. Carga futura terá limites explícitos, dataset Small, credenciais sintéticas, métricas e relatórios por endpoint. Taxa solicitada deve ser comparada com a linha temporal real: atingir max-workers pode atrasar chegadas e invalidar a conclusão de capacidade.

## Consequências, operação e riscos

- Custo: download/extração do SDK e cache de compilação locais, sem serviço pago ou componente em produção. Há manutenção do grafo do gerador; versões upstream novas não são promovidas automaticamente.
- Falha: ausência, alteração de bytes, build diferente, scanner indisponível ou achado em módulo presente bloqueiam o gate. Recuperação: conferir fonte/pins e reconstruir em execução nova; não ignorar TLS/scanner.
- Evidência: logs/manifesto local, hashes, TRX e resultados sintéticos. Não publicar artefatos automaticamente. O scanner não prova ausência de falhas desconhecidas nem substitui testes.
- Local: somente Windows x64 validado neste incremento. Produção: ferramenta roda em geradores isolados do serviço, após validar a plataforma correspondente; distribuição ainda não implementada.
- Remoção: trocar adapter/testes e bootstrap; domínio, API, banco e contratos não dependem do CLI. Preservar relatórios históricos e contratos de evidência.
- Reconsiderar: upstream com runtime/dependências corrigidos; necessidade de jornadas dependentes, renovação de tokens ou geração distribuída; custo de manter o overlay; gerador incapaz de sustentar a taxa sem saturar.

## Fontes primárias consultadas

[Vegeta e licença](https://github.com/tsenart/vegeta/tree/v12.13.0), [release e checksums](https://github.com/tsenart/vegeta/releases/tag/v12.13.0), [flags/TLS](https://github.com/tsenart/vegeta/blob/v12.13.0/attack.go), [política Go](https://go.dev/doc/devel/release), [SDK e hashes](https://go.dev/dl/), [govulncheck](https://pkg.go.dev/golang.org/x/vuln/cmd/govulncheck), [NBomber](https://nbomber.com/), [Gatling TLS](https://docs.gatling.io/reference/script/http/tls/), [Locust](https://docs.locust.io/en/stable/writing-a-locustfile.html). Consulta em 2026-09-21; decisão baseada no experimento local, não em alegações de throughput de terceiros.
