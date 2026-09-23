# Núcleo de administração de memberships

2026-09-23. Incremento sobre `8cb2a78`, Windows x64, .NET 10 Release e PostgreSQL 18.6 local descartável, TLS VerifyFull/SCRAM. max_connections=40/shared_buffers=64MB preservados; pools runtime=12/membership=8/operacional=5 no harness. Sem ambiente equivalente à produção.

## Escopo e evidências focadas

- `MemberAccessChangeTests`: delegação, versões, audit/recibo atômicos, replay reautorizado, RLS/grants e concorrência real.
- `MembershipAccessTests`: regras de domínio, audit antes/depois, validação e fingerprint independente de cultura.
- `DatasetTests`: Small com 1.050 vínculos/1.000 usuários/50 tenants/10.000 ordens; migration preserva conteúdo, limita lock e recusa Down destrutivo. Receita v2 não concede ManageMembers e inclui novas colunas/tabela no checksum.
- `MemberReadTests`: contrato aditivo version; autorização/paginação preservadas.
- `RuntimePrivilegeBoundaryTests`: leitura por coluna do histórico administrativo também é rejeitada pelo guard comum.

Primeiro ensaio: 21/21, `.artifacts/postgres/cd43252a0b5243e28357c40e6d95cc94/membership-administration/Usuario_DESKTOP-14J57J9_2026-09-23_16_54_47_net10.0.trx`. Revisão após cenários adicionais/contrato: 52/52, `.artifacts/postgres/162eb96c681749c7ac4de26e5608a6d6/membership-review/Usuario_DESKTOP-14J57J9_2026-09-23_17_03_43_net10.0.trx`. Ambos clusters encerrados pelo harness. Build Release sem warnings/erros.

## Integridade observada

Cem repetições da mesma chave/versão: um Applied e 99 Replayed, uma versão nova e um registro. Cem chaves distintas disputando a mesma versão: um Applied e 99 Conflict. Dois administradores com snapshots concorrentes retirando acesso/suspendendo a si mesmos: um Applied, um Conflict, um administrador ativo restante; contador de retry capturado. Chave concorrente entre alvos diferentes: um efeito e um conflito, sem duas versões aceitas para o mesmo recibo.

Comando pausado antes do snapshot é negado após commit de revogação da membership. Replay reautoriza ator. Falha real de constraint no histórico preserva permissões/versão anteriores e zero audit. Injeção controlada de 40001 verifica três tentativas/Busy; cancelamento não gera efeito. Testes de role distinta negam acesso a ordens, mutação de IDs/diretório/histórico e criação de membros; RLS protege leitura/escrita estrangeira mesmo sem filtro EF.

## Gate completo

Gate aprovado em 2026-09-23, 17:10:25–17:13:43 UTC−03. **213/213 testes** (31 domínio/arquitetura + 182 integração), zero falhas/ignorados; 26 casos das regras e 12 checks aprovados. Restore locked, auditorias NuGet transitiva/Go e Gitleaks sem achados reportados, formatação, build Release, testes PostgreSQL, modelos EF e diff aprovados. CI remoto, imagem e performance continuam pendentes.

Manifesto `.artifacts/validation/9a42de099d9741a5a7c62904a85a5586/manifest.json`; SHA256 `E11D39DC9C0108046B8D7D3139AAFA64CDF5B818927152A94CC4ED1DCF444053`. Snapshot da fonte: `A3BE4BF9108B1EDA0C50B032D5128FDDBA636D918644D87A1C9A7EE9993A3DA9`. TRX `...17_11_14_net10.0.trx` (31) e `...17_11_16_net10.0.trx` (182), hashes individuais no manifesto. Cluster `.artifacts/postgres/19f6001dc4ac4d4bb0218db8c0bd5b02` encerrado.

Migration AtomicMembershipAccess: **22,865 ms**, uma amostra local no dataset Small. Lock concorrente provocou recusa e nenhuma migration parcial; checksums dos dados anteriores permaneceram iguais após upgrade; Down destrutivo recusado. Esse tempo isolado não é benchmark de API nem valida duração em Medium/Large. Pools do banco sintético foram observados em 2→0 após limpeza de propriedade do teste.

Documentação de resultados foi finalizada após o snapshot; 141 entradas não Markdown (fontes/configurações/scripts/testes/migrations) foram reconferidas sem alteração contra os hashes do gate. Scan de segredos e diff repetidos antes do commit. Este relatório não autoriza release.

## Limites

Nenhum endpoint administrativo adicionado; 16 operações existentes permanecem. Host separado, audiência/MFA, guard administrativo, bootstrap/convites e gestão global ainda pendentes. Credencial dedicada é provisionada somente no cluster de teste; script de grants operacional não contém senha nem cria login. Serviços novos não são registrados na API comum.

SSI cobre o protocolo de comandos de membership; futuros escritores globais exigem análise/testes próprios. Operações já em voo podem se serializar antes de uma revogação. Downgrade é deliberadamente recusado quando perderia histórico, versão ou bit administrativo; usar evolução compatível. Rehearsal Small não prova zero downtime em volume maior.

VALIDADO: integridade/autorização nos cenários descritos. RPS/p50/p95/p99/error rate HTTP não medidos. PROJETADO: workload documentado de 1M ativos na janela. NÃO VALIDADO: capacidade de 1M, produção, backup/restore, CI remoto, imagens, stress/spike/soak e performance desta transação. Nenhuma extrapolação dos 100 concorrentes para usuários suportados.
