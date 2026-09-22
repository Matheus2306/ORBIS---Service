# Controllers — compatibilidade e auditoria da superfície

Data: 2026-09-22. Base Git `47fd152ea82cddfcd78bc501601bd5c40b524e31`, working tree identificada pelo snapshot abaixo. Resultado: **engenharia local aprovada**, produção e escala não aprovadas.

## Escopo e execução

Oito rotas de negócio movidas de Program para WorkOrdersController e WorkOrderTransitionsController; DTOs em Contracts; policy nomeada preserva sub/client_id/jti/iat, JWT e regras persistidas. Health checks/OpenAPI continuam nativos. Sem dependência, migration, grant ou contrato de domínio novo. A configuração de execução local e integração IdP permanecem pendentes.

Windows x64, SDK 10.0.302/runtime 10.0.10, PostgreSQL 18.6 efêmero com VerifyFull, runtime restrito/RLS; HTTPS real HTTP/1.1 e HTTP/2 nos testes existentes. Build Release com zero warnings/erros. Fixtures reais, incluindo Small uniforme/concentrado; não são benchmark HTTP com esse dataset.

1. Antes da mudança: 126/126 (25 +101), zero ignorados, build verde; `.artifacts/controller-baseline/9a7c1d1984d54ba7a840e9b15fdf4b9b`. Inventário EF sem conexão confirmou cinco migrations; aplicação das migrations verificada nos testes PostgreSQL.
2. Primeira migração: 135/137; `.artifacts/controller-validation/fcadfb72da5d487f9ce0636dcb54aee9`. Teste `cursor=` detectou200 indevido: MVC converte string vazia em null. Corrigido preservando400; ampliados casos whitespace/parâmetros repetidos. Teste de métricas detectou template MVC sem barra inicial; expectativa/documentação ajustadas, mantendo allowlist e proibição de dados sensíveis.
3. Gate final: **140/140** (25 domínio/arquitetura/cursor +115 integração), zero falhas/ignorados; 26 casos das regras e 12 checks aprovados. Restore locked, format, build, testes, auditorias NuGet/gerador, Gitleaks, alinhamento dos dois modelos EF e diff. PostgreSQL encerrado após execução.

Novos testes verificam todas as oito operações em controllers, policy/limiter e401 sem token; quatro claims ausentes403; JSON truncado, mass assignment, null e body vazio400 com ProblemDetails sem eco; query inválida400, media type inválido415; oito operações/contratos no OpenAPI Development. Testes anteriores de isolamento, RLS, idempotência,100 concorrentes, TLS e gerador permanecem verdes.

## Evidência

- Manifesto: `.artifacts/validation/4e9268aa4b624a9793c505be50b7edb5/manifest.json`.
- Início/fim: `2026-09-22T10:18:50.5127432-03:00` / `2026-09-22T10:22:41.3999158-03:00`.
- SHA256 manifesto: `C21C4B17B4EC32FBF6A19BE7CFBBA6C1F5AAD8D60323E4D7AFD47EF1ED37A926`.
- SHA256 snapshot: `CA9ADAF50CF501C2D181C487E07247B04E2C348FD6216A0AA5E83D9698787230`.
- TRX domínio `...10_20_02_net10.0.trx`: `83CA382E6A9FFD998EC8913BCE1B252676D14362F92F82A5439DD139E7C359AC`.
- TRX integração `...10_20_07_net10.0.trx`: `6F82ADE8DE259E9E1361B09F38003818AD59A4C4E5A2A5F3105EAF3EFD5BC0E7`.
- Cluster encerrado: `.artifacts/postgres/121055ddd9744249816bcfd23eac6481`.

Após o gate, somente documentação de resultados/estado foi atualizada; conferir hashes dos insumos não Markdown e repetir secret/diff antes do commit. Artefatos ignorados exigem revisão antes de compartilhamento.

## Limites

111 operações únicas catalogadas,11 implementadas;31 domínios planejados. Nenhuma carga HTTP/RPS/p95/p99, overhead MVC ou capacity delta medido. `http.route` mudou conforme ADR-014. CI remoto, SAST especializado, DAST, imagem, restore e implantação pendentes. Próxima capability P0: contexto tenant/usuário/permissões; depois provisionamento/memberships separados. Controllers concluídos não equivalem à expansão integral do anexo nem à API BASELINE READY.
