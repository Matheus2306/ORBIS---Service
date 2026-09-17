# Autonomia

## AUT-001 — selecionar a fundação

Problema: repositório vazio com requisitos amplos de produção. Evidência: inspeção local. Alternativas: distribuir serviços desde o início ou construir limites dentro de um processo. Decisão: monólito modular e foco inicial no isolamento. Motivo: transações locais e menor carga operacional; separar serviços exige evidência posterior. Medição: nenhuma comparação de throughput disponível. Intervenção humana: nenhuma.

## AUT-002 — dependência real sem Docker

Problema: Docker ausente no PATH. Investigação: PostgreSQL 18.6 instalado em Program Files. Decisão: usar binários existentes para cluster descartável no workspace, porta exclusiva e autenticação SCRAM; não conectar a bases existentes. Resultado de integração ainda pendente. Intervenção humana: nenhuma.
