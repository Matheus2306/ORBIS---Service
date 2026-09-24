# Administração HTTP — evidência local

2026-09-24. Base Git6ee4453 + incremento documentado pelo snapshot abaixo. Estado final:262/262 testes aprovados (33 unidade/arquitetura +229 integração), zero ignorados; 26 regras e12 checks do gate aprovados. Release sem warnings/errors. Não é aprovação de produção ou capacidade.

Artefato focado: `.artifacts/postgres/e2410e64b1624fffa332d3ba8e60b90a/administrative-http/Usuario_DESKTOP-14J57J9_2026-09-24_08_38_27_net10.0.trx`. PostgreSQL18.6 efêmero com TLS/SCRAM, role administrativa restrita; ambiente Performance na jornada HTTPS real. Discovery OIDC substituído pela chave RSA de teste, assinatura e demais validações reais. Cluster encerrado. Hardware/gerador não instrumentados para carga.

Cobertura: três controllers/contratos OpenAPI/policy/limiter/deadline; permissões/status parciais; replay/conflito/revogação/último admin; JWT errado, ausente, expirado, ID token, client indevido, audiência única e MFA recente; tenant/host/claim/alvo indevidos; mass assignment/JSON duplicado/permissões desconhecidas; credenciais/grants não intercambiáveis; drift e recuperação; bloqueio real de linha com503 e nenhuma alteração parcial, retry da mesma chave posteriormente confirmado. Compartilhamento do emissor/chave entre hosts torna o teste de audiência significativo.

Execuções de desenvolvimento preservadas: fe69a9bad1d24a319bd0c4ed84369c52 teve44 falhas no harness (seed sem tenant transaction; REVOKE de tabela removeu grants por coluna e contaminou casos seguintes), corrigidas; bff1599ae4b941dba39bd3defdb8cfe4 teve47/48, erro do teste ao mudar BaseAddress depois de enviar request, corrigido usando cliente separado. Não houve relaxamento do guard, TLS, MFA ou RLS para aprovar testes.

VALIDADO: comportamento funcional nos casos listados. PROJETADO: budgets e workload existentes. NÃO VALIDADO: RPS/p50/p95/p99, usuários simultâneos, regressão de performance dos dois hosts, IdP/MFA operacional, implantação/backup/restore,1.000.000 usuários. Sem migration adicional. Pipeline remoto/scans de imagem/DAST/soak/carga seguem gates pendentes.

Primeiro gate completo `f9be26666e784be59773390aaaca764d`: build/formatação aprovados; integração228/229 (1 falha), unidade33/33. Falha reproduzida: mapper compartilhado classificava constraint de audit encapsulada em EF como503, quebrando contrato500 existente. Correção diferencia PostgresException.IsTransient de violações permanentes e preserva rollback/erro sem detalhes. Esse manifesto é reprovado, não evidência final.

Gate final: `.artifacts/validation/a6c9cc40f9dd4a4598b71489742fc780/manifest.json`, status passed, productionApproved=false. SHA256 do manifesto: `5F349D5C2EDEAA021200E96584817EF9333B29CEEC4E2FB4285290C7A60B8AA1`; snapshot de fonte: `B3E36043BB65EE03E09ECE3138E6C1594D12CAAC97A86263A84B0033E2A7CDF6`. TRXs33/229 com hashes no manifesto. Inclui o regressivo de auditoria restaurado, comparação exata de audiência sem normalizar barra final, suíte comum e administrativa, migrations alinhadas, auditoria NuGet transitiva dos dez projetos, govulncheck e Gitleaks sem achados reportados. Cluster `f0df33b9255944f5bcf42df9667bc2d6` encerrado.

Publish local Release com `--no-build --no-restore`: `.artifacts/publish/6854adc1af56447a84c30196906853dd/{service,administration}`. Nenhuma distribuição contém o assembly do outro host. Artefatos locais não foram implantados; não houve push.

SHA256 do executável comum: `0851BCB056BA5E6B8C5BE9F6B1D54435C1B9677F3BD6F1F111DB4ECE621FE3E6`.

SHA256 do executável administrativo: `220F59802A1521F45A3EE9962DFF9F4AD9D0964DA0940FFE74F5EADE6E4C86C7`.

Após o gate, somente documentos Markdown receberam resultados/estado. Antes do commit, hashes de todos os insumos não Markdown devem permanecer iguais ao snapshot; diff e Gitleaks são repetidos. Não reclassificar essa verificação documental como novo teste funcional ou performance.
