# Progresso

## 2026-09-22 — controllers e auditoria de expansão

Oito rotas extraídas em dois controllers, DTOs separados e policy de claims preservada. Baseline 126 testes; migração revelou cursor vazio aceito por MVC e mudança de rótulo http.route. Corrigidos/documentados, 140 testes e 12 checks locais aprovados. Auditoria e catálogo de 111 operações para 31 domínios, 11 implementadas; matriz de autorização e sequência P0 definidas. Sem mudança de schema, sem nova capability de negócio e sem performance HTTP validada. Próximo: contexto tenant/identidade e fronteira administrativa.

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

## 2026-09-18 — transporte do banco para Performance

Cluster local agora exige TLS e SCRAM, com CA efêmera fornecida explicitamente aos clientes e sem confiança global. Chave do servidor em pasta protegida; CA privada não persistida. Build/114 testes aprovados, inclusive rejeição de plaintext/CA alheia/hostname incorreto e gate VerifyFull em Performance. Apenas Windows validado. Preservados números históricos de planos; nenhuma comparação de overhead ou capacidade inferida. Próximo: Kestrel/HTTPS e telemetria para carga HTTP real.

## 2026-09-21 — HTTPS real antes do benchmark

Retomado diagnóstico de handshake: logs comprovaram rejeição de chave efêmera pelo Schannel. Importação temporária UserKeySet corrigiu o harness sem instalar CA ou relaxar verificações. HTTP/1.1 e HTTP/2 exatos mantêm autenticação, isolamento e replay; raiz alheia/hostname incorreto rejeitados. Build, formatação, Gitleaks e 118 testes aprovados. Nenhum RPS/percentil HTTP medido. Próximo: telemetria nativa com atributos controlados e pool nomeado.

## 2026-09-21 — verificar sinais sem exportar dados indevidos

Pool da API nomeado orbis-runtime para não usar connection string como atributo. Teste HTTPS coleta instrumentos nativos por instância/pool, verifica rotas/status/durações/ocupação e ausência dos dados sensíveis exercitados. Build, 119 testes, formatação, Gitleaks e consulta NuGet transitiva aprovados. Inventário de observabilidade explicita que coleta externa/recursos/alertas e carga ainda faltam. Inventário local: sem k6, sem distribuição WSL, Java disponível é 8u281/32 bits; não usar esse runtime antigo para novo gerador. Nenhuma ferramenta de carga instalada.

## 2026-09-21 — evidência local reproduzível

Consolidados 11 checks em validate-local.ps1 com manifesto/hashes/estado Git e TRX novos. Auditoria JSON não depende apenas do exit code; pacotes/diagnósticos/incompletude bloqueiam. Testes ignorados, assemblies omitidas e fonte alterada também bloqueiam. Execução final aprovada: 119 testes, 21 regras, migrations alinhadas e scanners sem achados reportados. Scanner ausente foi rejeitado em ensaio separado. F6 segue em curso, sem CI remoto/imagem/perf smoke. Próximo: gerador e host Performance isolados para carga HTTPS.

## 2026-09-22 — gerador externo com evidência de transporte

Vegeta escolhido após comparação e experimento: binário oficial usa Go antigo com achados; rebuild final fixa SDK/dependências corrigidas e conserva versão upstream no build-info. Bootstrap em pasta nova reproduziu bytes, auditoria de módulos passou e manifesto antigo foi rejeitado após alteração de pins. Sete casos externos comprovam TLS/autorização/isolamento/replay com tokens por stdin e sem proxy/trust store global. Primeiro gate falhou por timeout no teste PostgreSQL/localhost; resultado preservado, teste tornou-se específico por IP + TargetHost sem relaxar VerifyFull. Seis testes focados passaram; gate final: 126 testes, 26 regras, 12 checks verdes. F5 ainda requer host separado, telemetria de recursos e baseline Small; produção/1M continuam não validados.
