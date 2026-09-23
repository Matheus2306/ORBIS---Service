# Autonomia

## AUT-020 — validar a boundary de credenciais antes de ampliá-la

Problema: administração requer credenciais com mais capacidades; guard atual só verificava parte dos privilégios efetivos. Investigação: documentação PostgreSQL18 e testes negativos reproduziram UPDATE por coluna e SET ROLE transitivo com USAGE=false, ambos aceitos no startup. Decisão: role comum sem memberships, matriz de tabela/coluna com grants necessários/proibidos e rejeição de delegação; não acrescentar credencial administrativa ao processo comum. Resultado: dois regressivos inicialmente falhando, 15 casos focados após correção, depois185 testes/12 checks/26 regras verdes. Nenhum grant produtivo foi alterado; clusters de teste encerrados. Administração planejada em ADR-015, sem alegação de implementação dos comandos. Intervenção humana: retomada solicitada; nenhuma decisão técnica exigiu confirmação.

## AUT-019 — consulta de membros e orçamento real de conexões do harness

Problema: administração não possuía consulta segura de vínculos. Decisão: ReadMembers explícita, DTO local mínimo, seek vinculado a tenant/ator/status, sem grants de escrita ou privilégios herdados de ordens. Migration Small com lock/statement timeouts e Down que falha sem descartar flags. Experimento: primeira suíte encontrou wrapping de SQLSTATE55P03 e esgotamento53300 por pools por banco sintético. Corrigida asserção exata; pools pertencentes a cada teste são encerrados e pg_stat_activity exige zero sessões restantes. Não elevar max_connections. Evidência: 30 testes focados, depois175/175, 26 regras e12 checks; 8→0/2→0 conexões; migration27,808ms no ensaio final, sem alegação de escala. Intervenção humana: retomada solicitada; nenhuma decisão técnica normal exigiu confirmação.

## AUT-018 — contexto sem ampliação de autoridade

Problema: portais não conseguiam obter identidade/organização/permissões atuais. Decisão: três GETs mínimos, resolvendo domínio/identidade e lendo membership sob RLS. Risco: consulta ao diretório enquanto a transação tenant segura conexão consumiria duas posições do pool. Implementação libera a primeira conexão antes da seguinte; teste real com pool1 passou. Evidência:153 testes/12 checks; sem alteração de grants/schema, sem capacidade de carga alegada. Intervenção humana: nenhuma.

## AUT-017 — preservar contratos na organização em controllers

Problema: Program concentrava transporte de oito operações; usuário solicitou controllers e anexou expansão funcional. Investigação: inventário real, baseline 126 testes e cinco migrations. Alternativas: Minimal APIs agrupadas ou MVC; escolhido MVC conforme pedido, sem alterar domínio/banco. Experimento: suíte detectou cursor vazio convertido a null e template de métrica sem barra inicial. Decisão: preservar400 para cursor inválido, exigir policy nomeada com claims completas, documentar rótulo MVC e manter componentes nativos de saúde/OpenAPI. Evidência: 140 testes/26 regras/12 checks; catálogo/matriz distinguem planejamento de implementação. Intervenção humana: retomada solicitada, nenhuma decisão técnica exigiu confirmação.

## AUT-001 — selecionar a fundação

Problema: repositório vazio com requisitos amplos de produção. Evidência: inspeção local. Alternativas: distribuir serviços desde o início ou construir limites dentro de um processo. Decisão: monólito modular e foco inicial no isolamento. Motivo: transações locais e menor carga operacional; separar serviços exige evidência posterior. Medição: nenhuma comparação de throughput disponível. Intervenção humana: nenhuma.

## AUT-002 — dependência real sem Docker

Problema: Docker ausente no PATH. Investigação: PostgreSQL 18.6 instalado em Program Files. Decisão: usar binários existentes para cluster descartável no workspace, porta exclusiva e autenticação SCRAM; não conectar a bases existentes. Resultado de integração ainda pendente. Intervenção humana: nenhuma.

## AUT-003 — espera do inicializador e integração

Problema: banco saudável em log, mas nenhum teste executado. Causa: Start-Process -Wait aguarda árvore incluindo PostgreSQL persistente. Correção: aguardar somente pg_ctl via WaitForExit com timeout, manter janela oculta e cleanup. Resultado: script concluiu, 24 testes aprovados e servidor encerrado. Intervenção humana: usuário retomou a execução após interrupção da sessão; nenhuma escolha técnica exigida.

## AUT-004 — alinhar EF Relational

Problema: ao usar APIs relacionais, build detectou assembly 10.0.4 transitivo versus 10.0.12 usado pelo design tooling. Decisão: referência direta e centralizada de EF Relational 10.0.12, sem suprimir warning. Nova medição: build com -warnaserror, zero avisos/erros, testes reais aprovados. Isso é correção de compatibilidade, não otimização de performance.

## AUT-005 — identidade e guard operacional

Problema: RLS no banco não prova autorização HTTP. Implementação: diretório persistido, JWT real, membership por request e autorização de recurso. Revisão independente apontou health anônimo amplificando consultas e falta de verificação de grants de membership. Decisão: health TTL 5s/single-flight/limite 4; startup sempre novo; guard nega mutação de membership. Evidência: 54 testes reais aprovados, incluindo drift/recuperação. Não foi medido ganho de throughput. Intervenção humana: nenhuma decisão técnica exigida.

## AUT-006 — retry sem duplicidade

Problema: POST com resposta perdida ou repetição paralela. Alternativas: memória, lock externo, unicidade PostgreSQL. Decisão: PK tenant/ator/chave, fingerprint v1 e recibo/audit na mesma transação. Ajuste preventivo: timestamp alinhado a microssegundos para primeiro recibo e replay serem idênticos. Evidência: 75 testes aprovados, incluindo 100 comandos concorrentes e falha real do audit; nenhuma medição de capacidade. Intervenção humana: nenhuma.

## AUT-007 — navegação limitada com política única

Problema: lista precisa limitar transferência e manter autorização por recurso. Decisão: keyset por created_at/id, máximo 100 e ReadFilter compartilhado entre detalhe/lista no SQL. Experimento: empate de timestamps, inserção entre páginas e cursor de outro escopo; 86 testes aprovados. Nenhum índice novo ou ganho de throughput alegado antes do baseline. Intervenção humana: nenhuma.

## AUT-008 — transição única com recuperação do retry

Problema: aceite/conclusão simultâneos e resposta perdida. Decisão: versão otimista + recibo tenant/ator/ação/chave + audit da versão resultante, transacionados juntos. Conflito permite uma releitura para distinguir replay de comando divergente; timeout não sofre retry cego. Nova atribuição exige conta global ativa do prestador. Evidência: 98 testes, duas disputas de 100 conclusões com efeito único e rollback real de audit. Intervenção humana: usuário retomou a sessão; nenhuma decisão técnica dependia de permissão.

## AUT-009 — volume reproduzível antes da medição

Problema: consultas ainda só haviam sido verificadas em fixtures funcionais pequenos. Alternativas: dados manuais, entidades EF rastreadas, COPY streaming em ferramenta isolada. Decisão: receita determinística e COPY, sem dependência adicional nem alteração das fábricas de domínio. Evidência: 108 testes; hashes iguais em duas bases de cada perfil Small, recibos compatíveis com os comandos reais, RLS e rollback. Scripts de cluster e grants compartilhados evitam divergência entre testes e medições futuras. Medium/Large ainda não executados; nenhum ganho de capacidade alegado. Intervenção humana: nenhuma.

## AUT-010 — planos reais e limites da inferência

Problema: faltava evidência do caminho de paginação no PostgreSQL. Experimento: interceptar SQL/params dos leitores EF, preservar runtime/RLS e comparar páginas equivalentes com offset. Medição: 960 EXPLAINs medidos, dois perfis; no tenant com 3.000 ordens o keyset profundo leu 26 linhas, enquanto offset varreu 3.000. Decisão: manter índice temporal; não adicionar índices por ator ou cache sem carga representativa. Trinta amostras seriais/instrumentadas não provam p99 de API nem throughput. Intervenção humana: retomada após revisão automática indisponível por créditos; fluxo de aprovação respeitado.

## AUT-011 — aproximar transporte antes da carga

Problema: a API Performance já exigia VerifyFull, mas o cluster local anterior não fornecia TLS. Alternativas: reduzir validação (rejeitada), provisionar certificado público (sem necessidade local), CA efêmera explícita. Decisão: criptografia .NET nativa, SAN IP, hostssl obrigatório e pasta TLS com ACL restrita. Evidência: 114 testes, conexões negativas rejeitadas e API Performance funcional com VerifyFull; sem dependência nova, trust store ou benchmark inventado. Intervenção humana: nenhuma.

## AUT-012 — corrigir a causa do handshake

Problema: quatro testes Kestrel recebiam EOF antes de validar o certificado. Investigação: logs Debug confirmaram Schannel/0x8009030E e chave efêmera não suportada. Decisão: importação PKCS#12 em memória com contêiner temporário do usuário, sem PersistKeySet; buffer zerado e certificado liberado após servidor. Evidência: quatro casos antes falhavam e passaram; suíte completa 118/118, incluindo HTTP/2 e negações TLS. Nenhum teste foi enfraquecido. Intervenção humana: retomada da tarefa, nenhuma decisão técnica exigiu confirmação.

## AUT-013 — telemetria sem infraestrutura prematura

Problema: falta provar emissão e controlar atributos antes de coletar carga. Investigação: ASP.NET/Npgsql já fornecem instrumentos; nome padrão de pool deriva da conexão. Decisão: nome fixo e teste MeterListener, sem exporter/backend novo. Evidência: 119 testes, atributos HTTP/DB permitidos, dados exercitados ausentes e conexão liberada após jornada. Metadados internos de host/porta DB continuam restritos à operação. Não foi medida capacidade nem overhead. Intervenção humana: nenhuma.

## AUT-014 — automatizar verificação sem aprovar por omissão

Problema: repetição manual dos checks e scanners que podem retornar zero com achados. Decisão: script único, interpretação JSON/TRX, cobertura de projetos/assemblies, snapshots/hashes e status de engenharia local separado de produção. Experimento: scanner ausente, relatórios sintéticos inválidos e execução completa em cluster novo. Evidência: rejeição não zero do scanner ausente, 21 regras e 119 testes aprovados; 11 checks reais verdes. Intervenção humana: retomada solicitada durante a execução; nenhuma decisão técnica exigiu confirmação.

## AUT-015 — validar também a ferramenta que produzirá evidência

Problema: falta gerador HTTPS externo com CA por processo; binário oficial candidato usa runtime fora de suporte. Alternativas no ADR-013. Experimento: govulncheck encontrou 63 achados de símbolos no binário distribuído e dois após só recompilar. Decisão: módulo versionado intacto com SDK suportado, x/net/x/text corrigidos e locks/hashes fixados. Compilação em pasta nova reproduziu bytes; scanner no nível module não reportou achados. Sete testes externos e cinco regras novas validaram transporte e vínculo entre insumos/binário. Não há ganho de performance alegado. Intervenção humana: retomada solicitada, nenhuma decisão técnica exigiu confirmação.

## AUT-016 — não confundir timeout com segurança comprovada

Problema: gate de 2026-09-21 teve 125/126, por timeout no teste PostgreSQL de nome incorreto. Investigação: localhost resolve IPv6/IPv4, banco escuta IPv4; sem trace da falha não se afirmou causa exata. Decisão: conectar por IP e variar somente TargetHost TLS, mantendo validador, CA, VerifyFull e prazo. Evidência: seis casos focados e suíte final 126/126, 26 regras e 12 checks aprovados. Falha original e limites documentados. Intervenção humana: nenhuma decisão técnica exigiu confirmação.
