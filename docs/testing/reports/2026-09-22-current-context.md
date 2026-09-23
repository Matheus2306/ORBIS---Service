# Contexto de identidade/tenant — evidência local

2026-09-22. Base `e99b874`, incremento com três GETs: `/v1/me`, `/v1/me/permissions`, `/v1/tenant`. Contrato em `docs/api/current-context.md`. Sem schema, grants ou dependências novos; DTOs mínimos, RLS e autorização persistida em cada request. Credencial runtime continua sem administrar directory/memberships.

Release build: zero warnings/erros. Gate local: **153/153 testes** (25 domínio/arquitetura +128 integração), zero ignorados/falhas; 26 regras e12 checks. Treze casos novos verificam projeções, usuário multitenant, domínio não verificado/alheio, spoof/claim divergente, vínculo sem permissões, suspensão de vínculo/usuário/tenant, identidade desconhecida, alteração de permissões com token já emitido e pool máximo1. Contratos/OpenAPI cobrem11 rotas de negócio. Demais testes de ordens/TLS/concorrência continuam verdes.

Evidência: `.artifacts/validation/6a487e33733549f4a855d061f47f37d9/manifest.json`, SHA256 `AC7ACB8BCE96B26C81858CF4A965C9C2F3A37C1BF8CC75CF09F2CA0410A2BDC5`; snapshot `C5813580504341FD21B8802004AFDCF6216CD567E8BBDC7259019096C3D97F65`. TRX domínio `...10_39_38...`: `20BBE4576176A30B9AA45375D8282919C5A513025F9BF495126AC19118D779EE`; integração `...10_39_43...`: `EE164C9767D4FDF30EFD303BA125C33541A955C183CBC1B1E86D5595273C26F8`. PostgreSQL efêmero encerrado: `.artifacts/postgres/e58f155f94be41a18321d667836cc14a`.

Ambiente Windows/.NET10/PostgreSQL18, TLS VerifyFull e JWT real com discovery de teste. Não é IdP operacional nem carga HTTP. O teste com pool1 comprova ausência de retenção simultânea de duas conexões nesse fluxo, não throughput/latência. Sem benchmark ou overhead quantificado; metas em performance-budget permanecem não validadas.14 operações implementadas de111 catalogadas; P0 restantes43, P1 restantes42. Próximo: consulta de membros com permissão específica e paginação.

Depois do gate, apenas documentação/estado foi atualizada; conferir hashes não Markdown e secret/diff antes do commit. Produção e1M continuam não aprovados.
