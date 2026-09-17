# Progresso

## 2026-09-17 — abertura

Ambiente e sandbox inspecionados. Escrita de documentação dentro do PROJECT_ROOT valida a área permitida. Repositório inicializado. SDK e PostgreSQL identificados. Pesquisa em fontes oficiais de ASP.NET Core, EF e PostgreSQL realizada. Fase 0 em execução; nenhum gate de produção aprovado.

## Fundação do domínio

Produto, ADR-001 a ADR-010, workload, SLOs, banco, testes e readiness documentados antes do código. Solução com Domain/Application/Infrastructure/API e testes. Estados de ordem, acesso por membership/recurso, tenant obrigatório e limites de entrada implementados. Release build sem warnings/erros, 13 testes aprovados, formatação limpa, auditoria NuGet transitiva sem advisories reportados. Sem validação de banco ou capacidade ainda. Readiness explicitamente negativa evita confundir processo vivo com produto pronto.
