# Evidência de transporte PostgreSQL verificado

2026-09-18, incremento sobre `a3b032e`. Windows x64, SDK 10.0.302/runtime ASP.NET 10.0.10, PostgreSQL 18.6. Build Release sem warnings/erros, sintaxe PowerShell válida e **114/114 testes aprovados, zero falhas/ignorados**: 25 unitários/arquitetura/cursor + 89 integração. Formatação/Gitleaks sem achados reportados; dependências não mudaram após a auditoria NuGet do incremento anterior, sem advisories reportados naquela consulta.

Cluster descartável com ssl=on, protocolo mínimo TLSv1.2, hostssl/SCRAM em 127.0.0.1 e regra explícita hostnossl reject. Clientes Npgsql e psql usam VerifyFull/verify-full e CA específica. O teste consulta pg_stat_ssl e exige TLSv1.2 ou TLSv1.3, cipher presente e pelo menos 128 bits. Todos os testes anteriores de isolamento, concorrência, migrations, dataset e planos também executaram com esse transporte.

Ataques exercitados: credencial válida com SslMode.Disable recebe SQLSTATE 28000; CA alheia e hostname localhost ausente do SAN causam falha criptográfica AuthenticationException. API em ambiente Performance inicia e responde detalhe autorizado com VerifyFull. Alterar somente para Require impede startup com mensagem de exigência de verificação completa. TestServer continua sendo o transporte HTTP desta suíte; não é benchmark Kestrel/HTTPS.

CA/chaves produzidas com .NET, sem dependência ou instalação de certificado no sistema. CA privada descartada em memória; certificado leaf dura um dia. Inspeção de ACL da pasta tls confirmou herança desabilitada e somente usuário executor/SYSTEM com FullControl. Pedido de destino fora de `.artifacts/postgres/` foi rejeitado sem criar diretório. Não houve leitura/exportação de chave na verificação. Implementação Unix 0700/0600 não foi executada; não declarar portabilidade validada.

TRX em `.artifacts/postgres/2ddf399a1f1b4ed6b86036770fd0e8a7/test-results/`: unitário SHA-256 `103A5A0BA428DBD0248382F634F397FB5605C43AD3BC8C33BD0A49B3C82D4965`; integração `AEED0308FD99ECD1F3C631837528FDD8B91DE68281D896BCAF3FC9885AC5A6C4`. Integração ~90s, servidor encerrado. Isso não mede RPS/latência de API nem efeito do TLS na capacidade. Perfis de planos coletados no incremento anterior usaram o transporte local daquele momento; não alterar retroativamente seus números.

Próximos gates: Kestrel real, TLS HTTP, autenticação criptográfica, instrumentação de gerador/API/PostgreSQL, baseline de ≥5 minutos e métricas por endpoint. Certificados operacionais, revogação/rotação, HA/restore e produção continuam pendentes. **1M NÃO VALIDADO; produção NO-GO.**
