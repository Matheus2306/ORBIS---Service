# ADR-003 — PostgreSQL e EF Core 10

Status: adotado; PostgreSQL 18.6 local encontrado. Problema: transações, relações, integridade, consulta e isolamento. Requisitos: ACID, RLS, PITR, tooling .NET 10.

Alternativas: SQL Server atende transações, com licenciamento/operação diferentes; documento/NoSQL exige mais lógica para relações e invariantes; PostgreSQL oferece constraints, RLS e ecossistema adequado. Decisão: PostgreSQL 18 com Npgsql/EF Core 10. Sem Redis, réplicas, pooler ou particionamento iniciais. Leitura projetada e keyset com índices iniciados por tenant.

Evidência: relações do domínio e necessidade de atomicidade; não existe benchmark comparativo. Justificativa: reduz componentes e permite defesa em profundidade. Consequências: write primary é limite e dependência crítica. Runtime usa pool limitado; soma dos pools de réplicas da API precisa caber no orçamento do banco, preservando reserva operacional. Não aumentar max_connections como solução automática.

Riscos: filtros não protegem SQL bruto; RLS e testes são obrigatórios. Migrations fora do startup; expand/contract para N/N+1. Reconsiderar pooler após medir espera no pool e uso efetivo de conexões; replicas após medir leituras tolerantes a lag; particionamento após benchmark e plano de retenção. Fonte: [Npgsql EF 10](https://www.npgsql.org/efcore/release-notes/10.0.html).
