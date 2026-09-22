# Gerador HTTPS externo — evidência local

Data: 2026-09-22. **Gate local aprovado: 126/126 testes, zero falhas/ignorados** (25 domínio/arquitetura/cursor + 101 integração), mais 26 casos das regras de evidência. Não é baseline, release nem validação de escala.

Incremento sobre `e4192ff7c57463c062143574b31b149338f11982`, dirty=true. Snapshot SHA256 `21C132D00625795DEAF7A3081F0EC41FE4117EC6FAF12F973CDBB1EAB110D5D4`. Após o gate foram atualizados somente documentos de evidência/estado; código avaliado permanece igual. Windows x64, SDK .NET 10.0.302, PostgreSQL 18.6, Release.

## Escopo comprovado

Vegeta externo contra Kestrel/HTTPS em Performance e PostgreSQL VerifyFull. Sete testes enviam oito requisições: leitura 200, recurso alheio 404, host de outro tenant 404, assinatura inválida 401, CA alheia/nome incorreto rejeitados por x509, criação 201 e replay 201 com mesma Location. Exit code zero do CLI não aprova erros HTTP/TLS: cada resultado é interpretado. Um worker/request por execução, corpo não capturado, token somente por stdin, proxies removidos, loopback/porta dinâmica, sem confiança global.

Bootstrap em diretório novo reproduziu os bytes do experimento final. Go 1.27.1, Vegeta v12.13.0 identificado no build-info, x/net 0.56.0 e x/text 0.39.0; nenhum código upstream editado. SDK, módulos, locks, binário e scanner conferidos. govulncheck 1.8.0 consultou `https://vuln.go.dev`, base informando atualização em 2026-09-16, sem vulnerabilidades reportadas nos módulos presentes no executável. Não certifica módulos de desenvolvimento não incorporados nem falhas desconhecidas.

Cinco regras novas verificam insumos válidos e rejeitam ausência, checksum alterado, duplicidade e caminho inesperado. Ensaio real recusou o manifesto anterior após mudança de pins. Gate exige auditoria fresca, hashes e insumos correspondentes; não baixa/faz fallback automaticamente.

## Artefatos e hashes SHA256

| Artefato local | Resultado / SHA256 |
|---|---|
| `.artifacts/load-generator/1f7711a16f734e8d9b6fe3c866133fda/manifest.json` | aprovado; `BBE5E8DCA2FC3AD3C75C3A99CEAE35D0FA916175AF84EFD134DBA1FFC49054B3` |
| `vegeta.exe` nessa execução | `9A91280566763E024126CE5B3E036A6D0839EAC1FDDEF85F23A8AD80AA18663E` |
| `govulncheck.exe` nessa execução | `33AC9F913BC2B52D8374BE5E7B15279850166B5E0B305E0B6555BDDCA28B5A12` |
| `.artifacts/validation/900140ac47024e01bfbbf5679ef5cdd9/manifest.json` | aprovado; `A7215CE52A0AC6DBB46556EBA7F4CE67B23F34B8778F3F1CC457E7F0B1BDB040` |
| TRX unitário `08_17_43` nesse gate | `F6584E1A40C939F615E4F89BEEED1D83F0F5FC88E95B62CF3D9493C7A684DD33` |
| TRX integração `08_17_47` nesse gate | `6D5C026B9D27F2E4C3996EF54BA71CCCB2A99034973BA41B419E260BFBECFD1E` |

Bootstrap: 11:12:19–11:15:30 UTC. Gate: 11:15:59–11:20:46 UTC, 12 checks aprovados: regras, restore locked, ferramentas, auditoria do gerador, formato, build, testes, auditoria NuGet, Gitleaks, dois modelos EF e diff. Build sem warnings/errors; NuGet/Gitleaks sem achados reportados. PostgreSQL da execução `552bfc0639644592af3e4930cde2bd8a` encerrado. Artefatos locais preservados/ignorados pelo Git; revisar antes de compartilhar.

## Falha preservada e ajuste do teste

Gate anterior `.artifacts/validation/7f9733fbec8546f59b1722413bc8c92a`, em 2026-09-21, **reprovado**: 125/126. Os sete testes novos passaram; PostgreSQL com `Host=localhost` expirou em 5s, quando se esperava AuthenticationException. Nenhum timeout foi aceito como prova criptográfica.

localhost resolve ::1/127.0.0.1; o banco escuta somente IPv4. Sem trace original, não atribuímos o timeout definitivamente ao DNS/IPv6. O teste agora conecta por IP e altera somente TargetHost TLS para localhost, preservando CA, validador Npgsql, VerifyFull e timeout. Exige callback atingido e AuthenticationException. Os seis testes de transporte passaram no ensaio focado `c75fee9a5c564bdbaafa55c26363e304` e na suíte final.

Binário oficial antigo rejeitado: Go 1.22.12 e 63 achados de símbolos. Rebuild sem corrigir dependências deixou dois. O grafo corrigido eliminou achados reportados. Candidato com módulo principal `(devel)` foi substituído por compilação versionada, permitindo ao scanner identificar também o próprio Vegeta. Relatórios intermediários permanecem em `.artifacts/tools/go-build/`; não aprovam o binário final.

## Reprodução e limites

Executar `scripts/build-load-generator.ps1` e `scripts/validate-local.ps1`: [instruções](../../../performance-tests/vegeta/README.md), [ADR-013](../../architecture/adr/ADR-013-load-generator.md). Somente Windows x64 validado. Discovery OIDC é substituído por configuração de teste; assinatura/validação JWT reais permanecem. Ciclo operacional de IdP externo não medido.

**VALIDADO:** transporte/contratos e build local reproduzível. **PROJETADO:** 20k RPS na janela de 1M ativos. **NÃO VALIDADO:** baseline HTTP, recursos/limite do gerador, p95/p99 sob carga, knee point, noisy neighbor, distribuição, soak, produção e 1.000.000 usuários. Oito requisições funcionais não comprovam throughput sustentado ou SLO.
