# Auditoria e lacunas da API

Data: 2026-09-22. Base auditada: `47fd152`. Estado: **CORE / WORK ORDERS FOUNDATION**; API BASELINE READY, PRODUCTION READY e 1M SCALE VALIDATED não atingidos.

## Evidência e inventário

Inspecionados `src`, `tests`, `tools`, `scripts`, `infrastructure` e `docs`, contratos, permissões, migrations e ADRs existentes. Antes da alteração: oito operações de negócio em Minimal APIs no Program, dois health checks e um documento OpenAPI em Development; nenhum controller. Não existem outros controllers, jobs, webhooks ou endpoints de autenticação no produto. OpenAPI é JSON, sem Swagger UI.

- Aplicação: ReadWorkOrder, ListWorkOrders, CreateWorkOrder, TransitionWorkOrder e ResolveTenantUser; interfaces de leitura/escrita e resolução implementadas em Infrastructure. Sem mediator, validators externos ou handlers ocultos.
- Domínio: Tenant, TenantDomain, UserAccount, ExternalIdentity, Membership, Permission, WorkOrder, WorkOrderAudit; estados Requested/Assigned/Accepted/InProgress/Completed/Cancelled. Recibos de criação/transição em Persistence.
- Validação: invariantes no domínio/aplicação, DTOs rejeitam campos desconhecidos; JWT criptográfico, policy de claims, vínculo ativo e autorização de recurso; paginação keyset limitada.
- Banco: DirectoryDbContext com quatro tabelas globais; TenantDbContext com cinco tabelas protegidas por RLS forçada. Diretório somente leitura para runtime, memberships somente SELECT, auditoria/recibos sem UPDATE/DELETE. Não ampliar grants para implementar administração dentro da API comum.
- Migrations: InitialDirectory; InitialTenantBoundary; LinkDirectory; AtomicOrderCreation; AtomicOrderTransitions. Listagem EF `--no-connect` confirma inventário, não o estado de um banco operacional. Os testes aplicam migrations em PostgreSQL efêmero, incluindo evolução populada.
- Testes prévios: build Release sem warnings/erros; 25 testes de domínio/arquitetura/cursor e 101 de integração aprovados, zero ignorados. Evidência local: `.artifacts/controller-baseline/9a7c1d1984d54ba7a840e9b15fdf4b9b`; cluster encerrado. Incluem JWT, RLS, isolamento, concorrência, TLS e gerador externo. Não são benchmark HTTP.
- Infraestrutura: PostgreSQL local efêmero, gerador Small, planos SQL e gate local; nenhum ambiente produtivo implantado, backup/restore operacional ou IdP real validado. ADR-001/002/003/004/011/012/013 permanecem válidos.

## Lacunas por risco

| Severidade | Capacidade existente | Capacidade necessária / ausente | Prioridade e dependência |
|---|---|---|---|
| Critical | Validação JWT com discovery substituído apenas em testes | IdP operacional, onboarding verificável, MFA administrativo, revogação e recuperação end-to-end | P0; contrato ADR-004 antes de acesso público |
| Critical | Diretório/memberships lidos com privilégios mínimos | Provisionamento, convite, gestão de privilégios e separação Tenant Admin/Platform Admin | P0; control plane separado, auditoria, último administrador, não autoelevação |
| Critical | Startup nega banco/configuração insegura | Inicialização local documentada/reproduzível com conexão real, migrations e grants; inicialização direta hoje falha sem configuração | P0; não mascarar com usuário PostgreSQL privilegiado ou autenticação falsa |
| Critical | Testes locais verdes e runbooks desenhados | CI remoto, infraestrutura reproduzível, HA, segredos, restore/PITR medidos, deploy/rollback e alertas | P0 de release; ambiente operacional ainda não provisionado |
| High | Identidade e resolução internas | Contexto de usuário/tenant/permissões para os portais | P0; leitura limitada ao vínculo atual, sem revelar tenants alheios |
| High | Customer/Provider representados somente por UserId/membership | Perfis de cliente/prestador, contatos, competências, áreas e disponibilidade | P0/P1; separar perfil comercial de identidade global e preservar histórico |
| High | Ordem solicitada diretamente por descrição | Catálogo, categorias e ServiceRequest com triagem/aprovação/conversão | P0; migração aditiva, manter POST existente compatível |
| High | Execução básica e cancelamento antes de iniciar | Agenda, conflitos de horário, rejeição/reagendamento e tratamento operacional de exceções | P1; invariantes e concorrência antes das rotas |
| High | Auditoria transacional interna | Consulta administrativa filtrada e timeline pública sem dados internos | P1; permissões distintas e DTOs mínimos |
| High | Modelo SaaS documentado | Planos/subscriptions/entitlements/usage/quotas persistidos | P1; fonte transacional, downgrade e suspensão seguros |
| High | SQL Small medido; sem capacidade HTTP | Baseline por endpoint, hot tenant/noisy neighbor, stress/spike/soak/recuperação | P0 de release; gerador não pode ser gargalo |
| Medium | Sem arquivos, colaboração ou alertas ao usuário | Attachments com quarentena/ACL, comments e notificações in-app | P1; storage/retenção/jobs somente com decisão e testes |
| Medium | Listagem keyset de ordens | Busca autorizada, dashboard agregado e reports assíncronos | P1/P2; começar PostgreSQL, medir SQL/allocations/pool |
| Medium | Sem integração contratada | Webhooks assinados e integrações concretas | P2; SSRF, outbox, retry/idempotência e operação antes de ativar |
| Low | Sem branding avançado | Domínios personalizados e identidade visual configuráveis | P3; verificação de posse, TLS, lifecycle e cache isolado |

Critical aqui classifica bloqueio de capacidade/prontidão; não afirma vulnerabilidade explorável demonstrada.

## Sequência e gates

1. Preservar as rotas atuais em controllers finos; testar binding, claims, OpenAPI, concorrência e ataques existentes.
2. Contexto atual de tenant/identidade; depois provisionamento e memberships seguros. O acesso privilegiado fica separado antes de qualquer gestão de usuários.
3. Clientes → prestadores → catálogo → solicitações, com transações, paginação e DTOs próprios.
4. Completar workflow/agenda → anexos/comentários → notificações/auditoria → consultas agregadas → SaaS/control plane e integrações concretas.

Cada domínio exige autorização negativa, isolamento, migrations representativas, concorrência aplicável, OpenAPI, orçamento de performance e evidências. O catálogo é projeto de superfície, não promessa de implementação. [Matriz](domain-capability-matrix.md), [catálogo](endpoint-catalog.md) e [autorização](../security/authorization-matrix.md) controlam a expansão. Nenhuma criação massiva de rotas vazias.
