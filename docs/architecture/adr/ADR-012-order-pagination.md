# ADR-012 — paginação e autorização de leitura

Status: implementada e validada funcionalmente; planos Small medidos, performance HTTP pendente. Problema: navegação deve limitar trabalho/transferência e evitar deslocamento de páginas quando chegam novas ordens. Requisitos: isolamento, autorização antes do limite, ordenação determinística e máximo 100 itens.

Alternativas: offset é simples para acesso por número de página, mas pode deslocar itens sob inserções e percorrer prefixos longos; snapshot/export exigiria retenção e workflow próprio. Decisão: keyset descendente por (created_at,id), ambos imutáveis nos comandos atuais; default 25/máximo 100; buscar limit+1 sem COUNT global. Predicado por tupla traduzido pelo Npgsql; índice (tenant_id,created_at DESC,id DESC) já existe. Não adicionar índice por cliente/prestador sem query plans e medição.

Autorização: uma expressão de aplicação produz o filtro tanto para detalhe quanto lista. Membership fresco habilita ReadAllOrders, ReadOwnOrders ou ExecuteAssignedOrders; owner/provider aplicados no SQL. Filtros EF e RLS continuam em paralelo. Não buscar página ampla para depois remover recursos sem autorização.

Cursor v1: JSON limitado em Base64Url, posição e vínculo tenant/ator. Não é segredo nem credencial assinada. Validar versão, tamanho (512 chars), IDs e timestamp; comparar escopo com identidade resolvida. Mesmo cursor adulterado continua submetido ao filtro atual. Não consultar existência do ID do cursor. Se filtros/sorts forem acrescentados, versionar/incluir sua identidade; não reutilizar cursor silenciosamente em outra ordenação.

Semântica: nova inserção acima da posição não reaparece na próxima página; iniciar uma navegação nova para vê-la. Não existe snapshot consistente entre requests: alterações de autorização, inserções históricas e futuras exclusões podem mudar o conjunto. Export consistente terá contrato separado. Cliente trata cursor como opaco e nextCursor null como término; não faz aritmética de páginas.

Experimento: timestamps empatados, inserção após primeira página, cursor de tenant/ator alheio, adulteração, permissão de prestador e revogação, limites/formatos inválidos. Em 2026-09-18, planos Small sob RLS mostraram seek temporal de 26 linhas versus varredura/sort de 3.000 no offset profundo do tenant concentrado; páginas equivalentes verificadas. [Relatório SQL](../../performance/reports/2026-09-18-small-query-plans.md) inclui percentis instrumentados, sem ganho de API alegado. Cliente/prestador ainda usam índice de FK e sort; investigar Medium e carga antes de adicionar índices. Performance HTTP permanece pendente. Implementar snapshot apenas por necessidade de export, não para navegação interativa.

Fonte primária: [tradução de comparação por tupla no Npgsql](https://www.npgsql.org/efcore/mapping/translations.html#row-value-comparisons).
