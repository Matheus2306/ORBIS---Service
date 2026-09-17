# ADR-002 — isolamento por tenant

Status: adotado inicialmente; isolamento precisa de integração real para cada tabela nova.

Problema: acesso cross-tenant é crítico; 10 mil+ tenants tornam operações por tenant relevantes. Requisitos: negar leitura/escrita e inferência desnecessária, custo controlado, restauração e caminho para sharding.

| Alternativa | Vantagem | Custo/risco |
|---|---|---|
| Shared database/shared schema | Poucas migrations e pools; transações simples | Blast radius e noisy neighbor; restauração individual difícil |
| Schema por tenant | Namespace separado | Milhares de schemas/migrations; não elimina recursos compartilhados |
| Database por tenant | Backup e isolamento operacional dedicados | Provisionamento, pools, migrations e custo multiplicados |
| Híbrido/sharding | Move tenants quentes/enterprise | Diretório de placement, migração coordenada e recuperação complexos |

Decisão: shared schema com TenantId obrigatório, PK/FK compostas, filtros EF e RLS ENABLE + FORCE. Runtime sem superuser, BYPASSRLS, ownership, DDL ou TRUNCATE. Migração/backup são identidades operacionais distintas. Contexto tenant definido em transação com `set_config(..., true)`; nunca variável de sessão persistente. Ausência de contexto nega acesso. DbContext scoped, sem pooling de DbContext até testes específicos; pool de conexões permanece.

Host normalizado resolve diretório confiável; domínio é seletor, não credencial. Cruzar identidade validada, membership ativo, status do tenant, permissão e ownership do recurso. Header público TenantId nunca decide. Custom domain exige prova de posse, unicidade, TLS e remoção segura; não ativado inicialmente. Proxies aceitos explicitamente; não confiar em forwarded-host de qualquer remetente.

Consultas internas preservam tenant; caches/chaves/jobs/anexos/busca/exports/eventos deverão carregar escopo autenticado antes de implementação. Dados globais limitados ao diretório/identidade e sem listagem pública. Respostas de recurso alheio equivalentes a inexistente, sem erros de constraint expostos.

Consequências: RLS impede omissão acidental de filtro, mas não defende processo comprometido que pode definir outro tenant; não substitui autenticação/SQL parametrizado. PK/FK compostas impedem associação cruzada. Riscos: políticas ausentes, role errada, SQL bruto, contexto retido no pool, inferência por constraints e métricas. Testar cada um.

Benchmarks: nenhum. Placement futuro `Tenant → shard` sem joins globais entre tenants; não implementar roteamento fictício. Reconsiderar ao atingir 60–70% sustentados do orçamento medido de I/O/CPU/conexões, tenant dominando latência alheia, contrato de residência ou restore dedicado. Split exige freeze de escrita, cópia verificada, replay controlado, mudança atômica de diretório e rollback. Backup individual não é resolvido por shared schema.

Fonte: [PostgreSQL RLS](https://www.postgresql.org/docs/18/ddl-rowsecurity.html).
