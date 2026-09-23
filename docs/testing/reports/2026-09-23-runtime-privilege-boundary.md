# Fronteira de privilégios do runtime

2026-09-23. Base: c3d645d (consulta de membros, gate175 aprovado). Alteração motivada pela preparação da administração, sem nova rota, migration, role operacional ou privilégio concedido no produto.

## Reprodução anterior

Dois testes negativos executados contra o guard anterior falharam porque startup não rejeitou grants inseguros. PostgreSQL18.6 efêmero, TLS VerifyFull, build Release, sem alterações em bases existentes. TRX: `.artifacts/postgres/9c3f7a0fe6c841d691c3fbb6f65a1ec5/privilege-before/Usuario_DESKTOP-14J57J9_2026-09-23_15_53_30_net10.0.trx`; dois failures "No exception was thrown"; cluster encerrado.

- UPDATE(permissions) em memberships: has_table_privilege retornou false; has_any_column_privilege retornou true; UPDATE de uma linha sob RLS foi permitido e revertido na transação.
- Cadeia runtime→bridge→writer com INHERIT FALSE/SET TRUE: USAGE=false, SET=true; SET LOCAL ROLE assumiu writer. O guard via role atual permaneceu saudável.

Precondição: operador/infraestrutura concede privilégios incompatíveis com a API. Não foi encontrada exploração HTTP, SQL injection ou vazamento entre tenants nessa reprodução. A falha é na verificação preventiva da configuração. Scripts existentes não concediam esses privilégios.

## Correção e validação

Guard consulta membership transitiva, identidade de sessão, atributos/ownership e matriz explícita de capacidades nas nove tabelas, incluindo grants por coluna e WITH GRANT OPTION. Rejeita excesso e falta de grants. Runtime sem memberships é política deliberada, não equivalência entre MEMBER e privilégio imediatamente utilizável. ADR-015 detalha limitações e efeitos operacionais.

Verificação focada: 15/15 casos de RuntimePrivilegeBoundaryTests/RuntimeDatabaseCheckTests aprovados, zero falhas/ignorados. Inclui os dois regressivos, oito configurações adicionais, quatro mutações de membership e drift/recuperação com 30 probes concorrentes. A cadeia de roles também é rejeitada quando SET/INHERIT são retirados mas ADMIN é concedido. TRX: `.artifacts/postgres/67cff6fb8dd54a23b2a8761db03bbb18/privilege-after/Usuario_DESKTOP-14J57J9_2026-09-23_15_58_04_net10.0.trx`; cluster encerrado. Build Release sem warnings/erros. Esse ensaio antecedeu o gate completo abaixo.

## Gate completo — aprovado

2026-09-23, 16:01–16:07 UTC−03. 185/185 testes: 27 unidade/arquitetura/cursor e 158 integração; zero falhas/ignorados. 26 casos das regras e todos os 12 checks aprovados: restore locked, ferramenta, auditoria Go, formato, build Release sem warnings/erros, testes PostgreSQL, auditoria NuGet transitiva dos oito projetos, Gitleaks, modelos EF de ambos contextos e diff, além do autoteste do gate. Scanners sem achados reportados. Não é resultado de CI remoto.

- Manifesto: `.artifacts/validation/9967d2cef39c4152820c864cb3c637c3/manifest.json`
- SHA256 manifesto: `F3AE177AF98B0CAA600DF918C6D8641106B48221E5B0C04FEE7384696222A3AF`
- SHA256 fonte avaliada: `6B3CAECBB102275CB12492C9D46CA0F933588D8B858FE67EEC86552125B853B8`
- TRX unidade: `Usuario_DESKTOP-14J57J9_2026-09-23_16_03_07_net10.0.trx`; SHA256 `5AB0A484EB40EF353036D4203FEF97F9889CC023B946523A7389A219877EFDEE`
- TRX integração: `Usuario_DESKTOP-14J57J9_2026-09-23_16_03_11_net10.0.trx`; SHA256 `0254ABC758549FB2F94D1C16CCDD766A851D1816E0442017F2F6DE2B785B65E6`

TRX em `test-results/` sob o manifesto. Cluster `.artifacts/postgres/bd0081ceb5cd49b0bef547bfb74283c8` encerrado. Fonte permaneceu estável durante o gate; somente documentação de resultados/estado mudou depois. Conferir hashes dos insumos não documentais, segredos e diff antes do commit. Sem migration/grant novo no produto; número de operações permanece16. A matriz de startup/readiness mudou intencionalmente e pode recusar configurações previamente aceitas.

## Limites

Readiness com TTL5s só sinaliza drift; não desfaz grants nem corta requests por conta própria. Não prova segurança de todo o cluster/funções privilegiadas nem capacidade de carga. Administração/IdP operacional, gates de release e validação1M permanecem pendentes.
