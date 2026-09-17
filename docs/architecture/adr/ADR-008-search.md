# ADR-008 — busca relacional primeiro

Status: candidato para módulo de busca ainda não implementado. Problema: encontrar ordens e catálogo com filtros tenant/status/data. Requisitos: escopo obrigatório, paginação, consistência previsível e custo controlado.

Alternativas: índices B-tree e busca exata; full-text/trigram PostgreSQL; Elasticsearch/OpenSearch. Decisão: filtros indexados e prefixos limitados inicialmente; medir EXPLAIN ANALYZE com buffers em Small/Medium. Full-text somente com relevância definida. Sem índice externo agora.

Evidência: requisitos iniciais estruturados; nenhum requisito de relevância multilíngue/fuzzy comprovado. Consequências: carga compartilhada com OLTP. Riscos: LIKE indiscriminado, count global, timing de existência alheia e índices excessivos. Reconsiderar com query p99 fora do budget após otimização ou exigência de busca não atendida. Qualquer índice externo deve ter tenant obrigatório em escrita/consulta e processo de purge/rebuild/ACL.
