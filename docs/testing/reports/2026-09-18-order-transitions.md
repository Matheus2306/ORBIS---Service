# Evidência de transições autorizadas

Data: 2026-09-18. Incremento sobre `6473d15`. Windows x64, SDK 10.0.302/runtime ASP.NET 10.0.10, EF 10.0.12/Npgsql EF 10.0.3, PostgreSQL 18.6. Cluster efêmero loopback/SCRAM, max_connections 40, shared_buffers 64MB, pool runtime 12/admin 5; API em Testing com JWT criptográfico e discovery estático. Sem equivalência de produção.

**98/98 testes aprovados, zero falhas/ignorados**: 25 domínio/arquitetura/cursor e 73 integração/HTTP. Release build sem avisos/erros; formatação aprovada; EF modelo alinhado; NuGet transitivo sem advisories reportados em 2026-09-18; Gitleaks 8.30.1 working tree sem leaks. Não houve nova revisão independente; revisão sequencial do delta e ataques automatizados não equivalem a auditoria completa.

Jornada HTTP: criação → atribuição → aceite → início → conclusão, com versões 1–5 e exatamente um audit por versão, atores corretos e recibos estáveis. Outro prestador não aceita a ordem; cliente não atribui; cancelamento depois do início retorna conflito; segunda conclusão com nova chave não altera estado. Cancelamento próprio permitido antes de iniciar; ordem de outro cliente é negada. Tenant/ID/prestador estrangeiros, prestador global suspenso e replay após revogar membership são negados. Versão velha, mudança de payload e salto de estado não gravam efeitos.

Cem conclusões concorrentes, mesma chave: um Applied e 99 Replayed. Cem conclusões com chaves distintas e mesma versão original: um Applied e 99 Conflict. Nos dois casos: versão final 5, uma auditoria e um recibo. São chamadas concorrentes ao comando com banco real, não 100 sessões HTTP de produção nem medição de RPS.

Falha real imposta por constraint de auditoria: HTTP 500 sem nome da constraint; ordem permanece InProgress/versão 4, sem audit/recibo parcial. Remover a falha e repetir mesma chave conclui a ordem. Isso não simula perda de rede no instante do commit. RLS nega leitura sem contexto, oculta recibo de A em B e rejeita INSERT cruzado; runtime não pode UPDATE/DELETE/TRUNCATE do recibo. Upgrade parte de audit no schema anterior e preserva seu ator/ação com order_version=1.

Artefatos `.artifacts/postgres/9c77fbfe6853464aa3aaaaa7346e8ca5/test-results/`. SHA-256 unitário `790B386EA9E445AD5007837ECF08FEE08496F2C12A0B509B81977B50619B02E4`; integração `AE4AA6883A2C7A75811E455C1E6140D5FF2C4892F0CAE199C9B7F61A7C4134E5`. Cluster encerrado. Integração aproximadamente 15s; não interpretar como percentil ou capacidade.

Ainda pendentes: Small/Medium/Large, latências/RPS/métricas de recursos, impacto de noisy neighbor, migração com audit volumoso, N/N+1, commit incerto, backup/restore, IdP operacional, UI e CI. Produção NO-GO; 1M NÃO VALIDADO.
