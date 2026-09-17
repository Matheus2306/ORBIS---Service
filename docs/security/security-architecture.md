# Arquitetura de segurança

Negar por padrão em todas as fronteiras. DTOs não recebem tenant, permissões ou actor confiáveis do cliente. API independente valida formato/tamanho e delega regras. Tenant selecionado por domínio exato verificado, identidade por issuer+subject; membership ativo e status do tenant rechecados antes de acesso. Recursos de terceiros retornam resposta indistinguível de inexistente. Sem log de payload, token, cookie ou documento.

Persistência: contexto explícito, filtros EF e RLS; transação curta, TenantId imutável, FKs compostas. Runtime sem DDL/owner/BYPASSRLS. Credenciais de migração/backup fora da API. Consultas parametrizadas e limites máximos de paginação. Sem Dynamic SQL concatenando entrada.

Configuração: fontes externas sobre defaults não secretos. Testing e Development isolados. Em Production falhar no startup se configuração obrigatória estiver ausente ou insegura. Não habilitar autenticação fake por header em runtime de produção. Proxy headers apenas de remetentes permitidos. Endpoints de health não retornam topologia/segredos. OpenAPI público somente se política decidir; desenvolvimento pode expor contrato.

Observabilidade: trace ID gerado/validado, logs estruturados com IDs mínimos e acesso restrito; tenant ID não é label de alta cardinalidade em todas as métricas. Audit transacional separado de application logs, append-only para runtime, retenção definida antes da release. Exportações são recursos tenant-scoped com expiração e reautorização no download.

Segredos: cofre no destino escolhido, identidades curtas quando possível, rotação ensaiada e logs redigidos. Backups criptografados e acesso segregado. Proteção de dados exige inventário, finalidade, retenção e fluxo de exclusão/portabilidade aprovados antes de produção; nenhuma conformidade legal é afirmada aqui.
