# ADR-005 — iniciar sem cache de negócio

Status: adotado. Problema potencial: leituras repetidas; não há gargalo medido. Requisitos: autorização fresca, consistência e isolamento.

Alternativas: sem cache → IMemoryCache limitado → cache distribuído → Redis. Decisão: consultas indexadas sem cache. Evidência: ainda não existe carga de aplicação; introduzir cache impediria baseline limpo. Experimento: medir endpoints, query plans e allocations antes/depois de eventual índice/projeção.

Consequências: mais leituras ao banco; menos invalidação e pontos de falha. Riscos: hot tenant; limites de concorrência serão medidos. Reconsiderar após p95 acima do budget e profiling comprovar leituras repetidas caras. Nova ADR precisará definir chave com tenant+versão+autorização, TTL, tamanho, stampede, eviction, invalidação, fallback e teste de indisponibilidade. Nenhum benchmark de hit ratio existe; métrica é N/A agora.
