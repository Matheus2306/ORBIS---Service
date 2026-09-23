# Arquitetura de segurança

Negar por padrão em todas as fronteiras. DTOs não recebem tenant, permissões ou actor confiáveis do cliente. API independente valida formato/tamanho e delega regras. Tenant selecionado por domínio exato verificado, identidade por issuer+subject; membership ativo e status do tenant rechecados antes de acesso. Recursos de terceiros retornam resposta indistinguível de inexistente. Sem log de payload, token, cookie ou documento.

Persistência: contexto explícito, filtros EF e RLS; transação curta, TenantId imutável, FKs compostas. Runtime sem DDL/owner/BYPASSRLS. Credenciais de migração/backup fora da API. Consultas parametrizadas e limites máximos de paginação. Sem Dynamic SQL concatenando entrada.

O [ADR-015](../architecture/adr/ADR-015-administrative-boundary.md) separa a futura credencial administrativa do processo da API comum. Startup/readiness verificam matriz explícita de grants necessários/proibidos, inclusive por coluna, delegação WITH GRANT OPTION e memberships transitivas em roles. Role comum não pertence a outra role, mesmo sem INHERIT/SET. Duas configurações antes aceitas foram reproduzidas em PostgreSQL isolado; [evidência](../testing/reports/2026-09-23-runtime-privilege-boundary.md). Readiness sinaliza drift após TTL; retirar tráfego e corrigir grants depende da operação, não de revogação automática pela aplicação.

Transporte PostgreSQL: a API exige VerifyFull fora de Development/Testing. O cluster descartável local também exige TLS no servidor e valida cadeia/nome com CA efêmera explícita, sem alterar confiança do sistema. [Configuração e limites locais](../testing/local-postgres-tls.md). Isso não substitui gestão/rotação de certificados de produção nem valida a borda HTTPS pública.

Configuração: fontes externas sobre defaults não secretos. Testing e Development isolados. Em Production falhar no startup se configuração obrigatória estiver ausente ou insegura. Não habilitar autenticação fake por header em runtime de produção. Proxy headers apenas de remetentes permitidos. Endpoints de health não retornam topologia/segredos. OpenAPI público somente se política decidir; desenvolvimento pode expor contrato.

Observabilidade: trace ID gerado/validado, logs estruturados com IDs mínimos e acesso restrito; tenant ID não é label de alta cardinalidade em todas as métricas. Audit transacional separado de application logs, append-only para runtime, retenção definida antes da release. Exportações são recursos tenant-scoped com expiração e reautorização no download.

Métricas HTTP/DB usam templates de rota e nome fixo de pool `orbis-runtime`, sem connection string como label. Teste real HTTPS verifica atributos permitidos e ausência de conteúdo/identificadores sensíveis nos casos exercitados. [Limites e coleta pendente](../operations/observability.md); exportadores e traces exigem revisão própria antes de ativar.

Segredos: cofre no destino escolhido, identidades curtas quando possível, rotação ensaiada e logs redigidos. Backups criptografados e acesso segregado. Proteção de dados exige inventário, finalidade, retenção e fluxo de exclusão/portabilidade aprovados antes de produção; nenhuma conformidade legal é afirmada aqui.
