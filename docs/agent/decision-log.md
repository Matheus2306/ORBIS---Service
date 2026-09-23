# Registro de decisões

| Data | Decisão | Evidência / consequência |
|---|---|---|
| 2026-09-23 | Serializable + versão + audit/recibo para administração de vínculos | ADR-016;213 testes/12 checks; último admin preservado entre snapshots concorrentes, role dedicada e serviços ausentes da API comum; HTTP/MFA ainda pendentes |
| 2026-09-23 | Receita sintética v2 inclui versão/histórico administrativo | Duas importações de cada perfil com conteúdo idêntico; sem concessão automática; IDs/hashes/evidências v1 não são baseline comparável |
| 2026-09-23 | Role comum sem memberships e matriz de grants por tabela/coluna | Dois bypasses de configuração reproduzidos, depois15 casos focados e185 testes verdes; futuro host administrativo separado no ADR-015 |
| 2026-09-23 | ReadMembers separada de flags de ordens; seek sem enumeração global | 19 cenários de integração de membros, cursor/contratos; migration Small preserva flags, Down recusa perda; 175 testes verdes |
| 2026-09-23 | Encerrar pools dos bancos sintéticos ao fim de cada teste | Falhas SQLSTATE53300 no gate inicial; medição 8→0/2→0 após limpeza por proprietário; limite40 e pooling da API mantidos |
| 2026-09-22 | Contexto atual exige vínculo ativo, inclusive para metadados | Três GETs, sem enumeração global de tenants; membro sem flags só lê contexto, não ordens;153 testes verdes |
| 2026-09-22 | Controllers por capacidade, DTOs separados e policy nomeada | ADR-014; rotas/segurança preservadas, diferenças MVC cobertas por 140 testes; rótulo de métrica documentado |
| 2026-09-22 | Expansão orientada por capability, não quantidade de rotas | 31 domínios, 111 operações catalogadas/11 implementadas, 46 P0/42 P1 restantes; control plane separado antes de administração |
| 2026-09-17 | Raiz é diretório atual, inicialmente vazio | Inspeção local; sem trabalho anterior a preservar |
| 2026-09-17 | Git em main; SDK 10.0.302 | Ferramentas verificadas; versão reproduzível será fixada |
| 2026-09-17 | PostgreSQL real para integração | Binário 18.6 existente fora do PATH; criar cluster exclusivo local, sem usar serviço ou banco existente |
| 2026-09-17 | Monólito modular + banco compartilhado com RLS | ADR-001/002; hipótese de custo operacional, não benchmark |
| 2026-09-17 | Adiar infraestrutura adicional | Nenhum gargalo medido que a justifique |
| 2026-09-17 | Perfil JWT estrito at+jwt e identidade issuer+subject | API independente, validação criptográfica real em testes; IdP operacional ainda pendente |
| 2026-09-17 | Diretório global SELECT-only e memberships tenant-scoped | Host verificado não concede permissão; legado migrado suspenso |
| 2026-09-17 | Readiness com TTL 5s e execução única | Contém amplificação de probe; não é cache de autorização nem evidência de throughput |
| 2026-09-17 | Idempotência por unicidade/transação PostgreSQL | ADR-011; 100 concorrentes, um efeito; sem lock distribuído ou broker |
| 2026-09-17 | Keyset e filtro de autorização compartilhado no SQL | ADR-012; integridade de navegação testada; query plans/ganho de performance não medidos |
| 2026-09-18 | Transições com versão esperada e recibo por ação | ADR-011 estendida; ator/permissão revalidados no replay; 98 testes aprovados |
| 2026-09-18 | Gerador separado, COPY streaming e manifesto do conteúdo | Small reproduzido em duas bases/perfil; transação exclusiva só no preparo; nenhuma alteração no runtime para facilitar carga |
| 2026-09-18 | Preservar índices após sondagem Small | Seek do despachante usa índice temporal; FK/sort de cliente/prestador ainda sem gargalo HTTP comprovado; relatório SQL com 960 amostras |
| 2026-09-18 | TLS obrigatório também no cluster descartável | Mantém VerifyFull exigido pela API Performance; CA local explícita e testes negativos, sem relaxar validação ou instalar confiança global |
| 2026-09-21 | Kestrel real/HTTPS com confiança explícita por cliente | Schannel exige chave em contêiner temporário; correção somente no harness, HTTP1.1/2 e rejeições criptográficas comprovados em 118 testes |
| 2026-09-21 | Métricas nativas e nome fixo de pool | Instrumentação existente atende emissão inicial; 119 testes verificam rotas/status/pool sem atributos sensíveis exercitados; exportador adiado até definir destino |
| 2026-09-21 | Gate local interpreta evidências e falha fechado | 11 checks, snapshot estável, 119 testes e 21 regras aprovados; ausência de scanner rejeitada; não equivale a CI remoto ou release |
| 2026-09-22 | Vegeta externo, recompilado com dependências corrigidas e versão upstream preservada | ADR-013; binário/insumos reproduzíveis, auditoria Go e 7 casos HTTPS aprovados; sem capacidade medida |
| 2026-09-22 | Separar validação do nome TLS de DNS/IPv6 no teste PostgreSQL | Timeout anterior reprovou o gate; conexão por IP + TargetHost errado mantém VerifyFull/AuthenticationException, 6 testes focados e suíte final verdes |
