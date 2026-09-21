# Evidência do gate local de engenharia

2026-09-21, Windows x64/.NET SDK 10.0.302/runtime 10.0.10/PostgreSQL 18.6. Incremento sobre `8938aa8`, executado com alterações ainda não commitadas (`dirty=true`). Manifesto identifica o conteúdo avaliado por hash; esta documentação do resultado foi adicionada depois. Não é uma execução do commit base puro.

Comando: `./scripts/validate-local.ps1`. Execução final entre **11:46:41 e 11:49:24 UTC**. Todos os 11 checks nativos retornaram zero: regras do gate, restore locked/sem HTTP cache, tool restore, formatação, build Release, testes, auditoria NuGet transitiva, Gitleaks, modelo Directory, modelo Tenant e diff. Os relatórios foram interpretados adicionalmente, e o snapshot de fontes permaneceu estável durante a execução.

**119/119 testes da aplicação aprovados**, zero falhas/ignorados: 25 unitários/arquitetura e 94 integração. **21 casos separados das regras do gate aprovados**: evidência incompleta, projetos/assemblies divergentes, testes ignorados/abortados, diagnósticos do feed e vulnerabilidades diretas/transitivas, inclusive com detalhes ausentes, são rejeitados. Esses 21 não são testes da aplicação nem carga. Um ensaio de scanner ausente também terminou com exit code não zero e manifesto failed, sem pular a etapa.

## Integridade dos artefatos

Manifesto final: `.artifacts/validation/d1ee4c3dfb86414082f03786e060736b/manifest.json`, SHA-256 `10E3517B02C6C2648B8785CA404B78E05F7854720E34F43007C936E3856C5744`. Digest do snapshot de fontes: `256F1806E8EC01D038776E41C50852D7CE32FE20D1E5FBF0EDB8F7843A46CEDA`. Hashes individuais das fontes/checks constam do manifesto. Isso permite conferência de conteúdo; não é assinatura nem atestado de um CI confiável.

TRX no subdiretório test-results: unitário `08_47_27`, SHA-256 `A20DCAB83BA2EFF90A3B5F7ADEA2B7A780C650D84374A448213E311C7AF5AB13`; integração `08_47_30`, SHA-256 `0ED461EA7CA58673490209586A6578A5E6319D9739310CD43D1BA63DBC819354`. Integração durou 1m29s. Cluster `.artifacts/postgres/8e7493c8ebca414180a8e25745e56ee6` encerrado; sem alteração de banco existente.

Gitleaks 8.30.1, executável SHA-256 `17157E2EE8B76FC8B1D8BEE607A250E34B8A8023C8BC81822D4B5EE4D78FCB7C`, nenhum leak reportado nas fontes. NuGet JSON v1 cobriu os oito projetos e nenhuma vulnerabilidade foi reportada. Build sem warnings/erros. Ambos modelos EF sem alterações pendentes. Relatórios locais não são automaticamente seguros para publicação.

O ensaio de scanner ausente está em `.artifacts/validation/e7197bd9f7d14ef1bd9949f472a6382b/manifest.json`. Uma execução intermediária, anterior aos dois casos adicionais de detalhes incompletos, também passou em `f2ce923ca2f24c4d8f43c7915e4c37dc`; usar o manifesto final acima para esta versão.

## Decisão

Gate **local-engineering aprovado**, `productionApproved=false`. F6 permanece em curso: CI remoto, artefato/imagem e scan, DAST e performance smoke ainda não foram validados. Carga HTTP, backup/restore, deploy/rollback, HA e IdP operacional também faltam. Nenhum RPS/p95/p99 de API foi medido. **Produção NO-GO; 1M NÃO VALIDADO.**
