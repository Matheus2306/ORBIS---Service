# Registro de decisões

| Data | Decisão | Evidência / consequência |
|---|---|---|
| 2026-09-17 | Raiz é diretório atual, inicialmente vazio | Inspeção local; sem trabalho anterior a preservar |
| 2026-09-17 | Git em main; SDK 10.0.302 | Ferramentas verificadas; versão reproduzível será fixada |
| 2026-09-17 | PostgreSQL real para integração | Binário 18.6 existente fora do PATH; criar cluster exclusivo local, sem usar serviço ou banco existente |
| 2026-09-17 | Monólito modular + banco compartilhado com RLS | ADR-001/002; hipótese de custo operacional, não benchmark |
| 2026-09-17 | Adiar infraestrutura adicional | Nenhum gargalo medido que a justifique |
