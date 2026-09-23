# Consulta administrativa de memberships — validação

Data: 2026-09-23. Base Git: 1876cd7. Incremento de leitura P0, sem concessão automática de acesso. Commit final consultável no histórico; gate avalia snapshot anterior à atualização documental dos resultados.

## Escopo e critério

GET members/lista e detalhe, ReadMembers, cursor tenant/ator/status, filtros, DTOs mínimos e no-store. Flags de ordens não concedem acesso. Testes reais de JWT/claims, RLS, IDs/host/claims/cursor adulterados, revogação, formato SQL limitado e grants SELECT-only. Contra PostgreSQL 18.6 local efêmero, TLS VerifyFull, Release, SDK10.0.302. Não há mocks de banco, grants ampliados nem mecanismo de segurança desativado.

Migration amplia CHECK para bit128; Small com 1.000 usuários/50 tenants/1.050 memberships/10.000 ordens. Verificações: hash de memberships preservado, nenhum grant automático, unknown bits negados, espera por lock interrompida e Down que perderia permissão rejeitado atomicamente. Medição de DDL é pontual, não benchmark HTTP nem latência sob carga.

## Primeira execução — reprovada

Manifesto `.artifacts/validation/de6d571084084fb2a0c640fbce75a193/manifest.json`. 27 testes de unidade aprovados; integração 111/148 aprovados, 37 falhas, zero ignorados. Uma asserção esperava PostgresException diretamente, mas NpgsqlExecutionStrategy encapsula o SQLSTATE 55P03 em InvalidOperationException. Corrigida para exigir a exceção interna exata e preservar o código de lock timeout.

Outras 36 falhas tiveram SQLSTATE 53300 (limite de conexões). Bancos novos dos testes de dataset mantinham pools ociosos até encerrar a coleção inteira. Alteração restrita ao harness: registrar conexões dos bancos criados por cada teste, liberar somente esses pools no DisposeAsync e verificar em pg_stat_activity que sobraram zero sessões. Observador sem pool, sem expor conexão ou credenciais. max_connections permanece 40; pooling e limites da API permanecem ativos. Evidência posterior registrará conexões antes/depois. Não atribuir essas falhas a capacidade da API sob carga.

## Verificação focada da correção

30/30 testes de MemberReadTests/DatasetTests aprovados, zero ignorados. TRX: `.artifacts/postgres/eff5533787d043b9bec87dce3e32b0ac/member-verification/Usuario_DESKTOP-14J57J9_2026-09-23_14_08_50_net10.0.trx`. Cluster encerrado. Cada par de bancos Uniform/HotTenant terminou com 8→0 conexões; os três testes de banco único, 2→0. Up da migration populada: 22,345 ms nesta execução; amostra única, sem concorrência nominal. Adicionada também asserção explícita do bit128 preservado após Down recusado; incluída no gate completo seguinte.

## Execução final

**Gate local aprovado** em 2026-09-23, 14:12–14:18 UTC−03. 175/175 testes (27 unidade/arquitetura/cursor + 148 integração), zero falhas/ignorados; 26 casos das regras do gate. Todos os 12 checks aprovados: restore locked, ferramenta fixada, auditoria Go, formato, build Release (0 warnings/erros), testes PostgreSQL, auditoria NuGet transitiva dos oito projetos, Gitleaks, dois modelos EF e diff, além do autoteste das regras. Não equivale a CI remoto verde.

- Manifesto: `.artifacts/validation/09fb37b9b7234a83a72ca8f0d15a045c/manifest.json`
- SHA256 manifesto: `5F93E290958AB3DF76A0264973EC5AA69F3E5780178719FF483CED1F379A3377`
- SHA256 snapshot fonte: `C10EC906C7DE9A93BCF56F2F1373FB2C11975C63D86D6B6CE59DD260395328B5`
- TRX unidade: `Usuario_DESKTOP-14J57J9_2026-09-23_14_13_42_net10.0.trx`; SHA256 `C267F96CE1BB79C4B95FAF969772DFBC56D37F17601A4E65B353601AA3C42439`
- TRX integração: `Usuario_DESKTOP-14J57J9_2026-09-23_14_13_49_net10.0.trx`; SHA256 `B6BB9F20DA3B3123AA92BD1FF85FB061403A719966E2ED424C3BB8E38146730B`

TRX em `test-results/` sob o diretório do manifesto. Cluster `.artifacts/postgres/7de7fec3854d44f689aa3136f47212e0` encerrado. A migração populada levou 27,808 ms neste segundo ensaio; mesma sequência de conexões 2→0, 8→0, 8→0, 2→0, 2→0. Não comparar percentualmente duas amostras como regressão/ganho. Permissão128 preservada após Down recusado; bloqueio concorrente cancelado por SQLSTATE55P03 e migration anterior mantida. Scanners NuGet/Go/segredos sem achados reportados nesta execução; revisão completa de segurança continua pendente.

Após o gate, apenas documentação de resultados/estado foi atualizada. Código/configuração conferidos contra hashes do manifesto e segredo/diff rechecados antes do commit. Artefatos locais ignorados pelo Git; preservar o diretório se for necessário auditar TRX/logs detalhados.

## Limitações

Sem medição HTTP de RPS/p95/p99, hot tenant, carga nominal ou escala. Scan de CHECK só ensaiado em Small; rehearsal representativo obrigatório antes de produção, sem promessa de zero downtime. Nenhum domínio com baseline produtivo completo; convites/administração de permissões/provisionamento/IdP e operação continuam no backlog. API BASELINE READY, PRODUCTION READY e 1M SCALE VALIDATED permanecem não atingidos.
