# Progresso

## 2026-09-17 — abertura

Ambiente e sandbox inspecionados. Escrita de documentação dentro do PROJECT_ROOT valida a área permitida. Repositório inicializado. SDK e PostgreSQL identificados. Pesquisa em fontes oficiais de ASP.NET Core, EF e PostgreSQL realizada. Fase 0 em execução; nenhum gate de produção aprovado.

## Fundação do domínio

Produto, ADR-001 a ADR-010, workload, SLOs, banco, testes e readiness documentados antes do código. Solução com Domain/Application/Infrastructure/API e testes. Estados de ordem, acesso por membership/recurso, tenant obrigatório e limites de entrada implementados. Release build sem warnings/erros, 13 testes aprovados, formatação limpa, auditoria NuGet transitiva sem advisories reportados. Sem validação de banco ou capacidade ainda. Readiness explicitamente negativa evita confundir processo vivo com produto pronto.

## Persistência e isolamento real

EF Core e migration inicial, RLS ENABLE/FORCE, filtros, FKs compostas, guards e versão de concorrência. PostgreSQL 18.6 efêmero com SCRAM e role runtime restrita. Após corrigir espera do inicializador no Windows, 24/24 testes passaram e cluster foi encerrado. NuGet e Gitleaks sem achados reportados. Evidência e limites em testing/reports. API de negócio ainda ausente; capacidade continua sem medição.

## Diretório e leitura autenticada

Identidade global issuer+subject, tenant/domínio verificado, membership fresco e GET com autorização de recurso. JWT real no teste, sem bypass. Upgrade preserva legado suspenso. Revisão independente concluída; guard contra grants excessivos e health com TTL/concorrência limitada. Build e 54/54 testes aprovados; dois modelos EF alinhados; scanners locais sem achados reportados. IdP real, portais, comandos e performance permanecem pendentes.

## Criação idempotente e auditoria

POST com autoridade derivada da sessão, rejeição de campos extras, chave obrigatória e recibo estável. Ordem/recibo/audit atômicos; RLS e append-only no histórico. Cem comandos concorrentes resultaram em uma criação; falha real do audit fez rollback completo. Build e 75/75 testes aprovados, migration alinhada, Gitleaks sem leaks. Sem ganho de performance alegado. Próximo caminho de leitura: lista keyset para depois medir workload representativo.

## Listagem com posição e autorização no SQL

GET de lista limitado a 100 itens com cursor v1 vinculado ao tenant/ator, seek por timestamp/ID e regra de leitura compartilhada com detalhe. Sem count global ou autorização pós-paginação. Build e 86/86 testes aprovados, incluindo empates, inserção entre páginas, cursor adversarial e acesso de prestador. Nenhum novo índice/cache; medição em volume ainda pendente.

## 2026-09-18 — transições e retomada

Retomado código de transições deixado antes da interrupção; sessão antiga de build não existia mais e o build foi refeito. Migration com RLS e audit versionado, backfill de registro anterior, cinco endpoints com autorização/versão/idempotência. Jornada HTTP completa e 100 conclusões concorrentes testadas; falha do audit preservou estado anterior. Build e 98/98 testes aprovados, scanners locais e modelo EF sem achados reportados. Próximo incremento: dados representativos e medição, sem classificar o núcleo como produto pronto.

## 2026-09-18 — dataset reproduzível

Gerador isolado da API com UUIDs/datas estáveis, perfis uniforme/hot tenant, usuários multi-membership, histórico e recibos. COPY streaming com checks/FKs; destino local vazio obrigatório, transação exclusiva e hashes do conteúdo persistido. CLI gera manifesto em cluster descartável. Build e 108 testes aprovados; reprodução byte a byte dos hashes em bases independentes, rollback e RLS/replay verificados. Small disponível; capacidade da API ainda não medida. Extraído ciclo de vida PostgreSQL e arquivo de grants para reuso, sem infraestrutura nova.

## 2026-09-18 — SQL observado antes de otimizar

Capturados 16 planos dos leitores EF reais e alternativa offset, role runtime/RLS, Small uniforme/concentrado, 30 amostras cada. Keyset profundo do despachante usa índice temporal e 26 linhas; offset concentrado varre/ordena 3.000. Cliente/prestador usam FK + sort: hipótese para volume maior, sem novo índice agora. Build/108 testes/scanners aprovados; artefatos e limites registrados. Revisão automática chegou a impedir uma execução por créditos ausentes; após retomada autorizou testes e coleta. Nenhum RPS/SLO HTTP ou 1M validado.
