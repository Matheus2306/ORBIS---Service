# Autonomia

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
