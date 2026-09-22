# ADR-014 — controllers por capacidade

Status: adotado em 2026-09-22 por solicitação do usuário; validação do incremento registrada no relatório de testes.

Problema/contexto: Program concentrava oito operações de ordens e o bootstrap. A expansão exige localizar contratos e autorização sem misturar configuração e transporte. Requisitos: preservar rotas, payloads JSON, Location relativa, status, idempotência, tenant boundary e limite compartilhado.

Alternativas: manter Minimal APIs agrupadas; extrair extension methods de endpoints; usar controllers com rotas por atributo. As três suportam o domínio; não existe benchmark comparativo que comprove superioridade de performance. Controllers atendem à organização solicitada e à descoberta OpenAPI nativa sem dependências novas.

Decisão: WorkOrdersController para leitura/criação; WorkOrderTransitionsController para cinco ações explícitas com tradução HTTP comum e aplicação das regras no serviço existente. DTOs em Contracts. Program registra MVC e mapeia controllers. Health checks e OpenAPI continuam nos componentes nativos, pois são infraestrutura, não regras de domínio. Não substituir o middleware de saúde por implementação manual.

IResult é mantido para preservar serialização JSON e respostas existentes; metadados de resposta são explícitos. MVC usa validação automática, com ProblemDetails genérico para erros de binding, sem eco de valores. Policy nomeada compartilha as claims da fallback policy; adicionar `[Authorize]` simples não seria equivalente. Nenhuma mudança de banco/grants/migrations.

Consequências/riscos: diferenças de binding de strings vazias, respostas 400/415 e template de métricas entre MVC e Minimal APIs precisam de testes. A métrica http.route de MVC não carrega a barra inicial; consumidores devem usar o template documentado. Custos de MVC ainda não foram medidos: nenhum ganho ou ausência de regressão HTTP é afirmado. A migração não corrige a falta de configuração de startup reportada anteriormente.

Reconsiderar: controller deixa de ser coeso ou passa a conter regras/queries; nesse caso dividir por capability. Só comparar desempenho em Release com dataset/workload reais. Documentação primária: [retornos HTTP em controllers](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0) e [validação automática](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0).
