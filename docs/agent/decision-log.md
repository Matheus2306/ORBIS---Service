# Registro de decisões

| Data | Decisão | Evidência / consequência |
|---|---|---|
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
