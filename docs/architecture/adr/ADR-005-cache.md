# ADR-005 — iniciar sem cache de negócio

Status: adotado. Problema potencial: leituras repetidas; não há gargalo medido. Requisitos: autorização fresca, consistência e isolamento.

Alternativas: sem cache → IMemoryCache limitado → cache distribuído → Redis. Decisão: consultas indexadas sem cache. Evidência: ainda não existe carga de aplicação; introduzir cache impediria baseline limpo. Experimento: medir endpoints, query plans e allocations antes/depois de eventual índice/projeção.

Exceção operacional implementada: readiness mantém apenas um resultado de saúde por instância por 5s, com uma probe concorrente e limite HTTP 4 sem fila. Evidência: revisão identificou consulta anônima ao catálogo por request disputando o mesmo pool; a limitação contém amplificação por construção, sem alegar ganho de throughput medido. Estados saudáveis e falhos expiram; alteração de privilégio é percebida na próxima probe após TTL, comprovada com banco real e relógio controlado. Não guarda dados de tenant, identidades ou permissões; não requer Redis/IMemoryCache. Startup sempre faz verificação nova. Removível no RuntimeDatabaseCheck sem alterar contratos de negócio.

Consequências: mais leituras ao banco; menos invalidação e pontos de falha. Riscos: hot tenant; limites de concorrência serão medidos. Reconsiderar após p95 acima do budget e profiling comprovar leituras repetidas caras. Nova ADR precisará definir chave com tenant+versão+autorização, TTL, tamanho, stampede, eviction, invalidação, fallback e teste de indisponibilidade. Nenhum benchmark de hit ratio existe; métrica é N/A agora.
