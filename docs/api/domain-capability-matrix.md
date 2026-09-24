# Matriz de capacidades por domínio

2026-09-22. **Parcial** significa que há código útil, não que o domínio está pronto. P0 fundamental; P1 baseline produtivo; P2 operação madura; P3 evolução. Nomes de entidades ausentes são candidatos e precisam de invariantes antes da migration. O catálogo detalha operações e contratos.

| Domínio | Estado | Prioridade | Entidades existentes ou propostas | Capabilities e endpoints candidatos |
|---|---|---|---|---|
| Identity | Parcial: contexto implementado | P0 | UserAccount, ExternalIdentity; UserPreference futura | `/me` implementado; login/MFA no IdP e preferências pendentes |
| Tenancy | Parcial: contexto implementado | P0 | Tenant, TenantDomain; TenantSettings futuro | `/tenant` implementado; settings/provisionamento pendentes |
| Membership | Parcial: consulta e comandos HTTP | P0 | Membership, MembershipAccessChange; Invitation futura | `/members` inclui version; núcleo de suspensão/reativação, audit e último admin testado; host/commands administrativos com MFA implementados; convites/provisionamento pendentes |
| Authorization | Parcial: gestão delegada HTTP | P0 | Permission flags; Role futura | `/me/permissions`, policies e autorização de recurso; ManageMembers/delegação em host separado com audiência/MFA próprias |
| Customers | Inexistente | P0 | Customer, CustomerContact | `/customers`, atualizar/arquivar/restaurar, histórico preservado |
| Providers | Parcial: designação por UserId | P0 | ProviderProfile, ProviderMembership | `/providers`, ativar/suspender; perfil tenant-local |
| Provider Skills | Inexistente | P1 | Skill, ProviderSkill | `/providers/{id}/skills`, competências do catálogo |
| Provider Availability | Inexistente | P1 | AvailabilityRule, AvailabilityException | `/providers/{id}/availability`, horários e exceções |
| Service Catalog | Inexistente | P0 | Service, ServiceVersion | `/services`, preço/moeda/duração/SLA versionados |
| Service Categories | Inexistente | P0 | ServiceCategory | `/service-categories`, hierarquia sem ciclo |
| Service Areas | Inexistente | P1 | ServiceArea, ProviderServiceArea | `/providers/{id}/service-areas`; critérios geográficos explícitos |
| Service Requests | Parcial: intenção embutida em WorkOrder.Requested | P0 | ServiceRequest, RequestDecision | `/service-requests`, aprovar/rejeitar/converter uma única vez |
| Work Orders | Parcial | P0/P1 | WorkOrder, WorkOrderAudit, receipts | Oito operações existentes; timeline, rejeição e reatribuição dependem de state machine revisada |
| Scheduling | Inexistente | P1 | Calendar, ReservationPolicy | `/availability/slots`, `/calendar`; timezone, intervalo semiaberto e capacidade |
| Appointments | Inexistente | P1 | Appointment | `/appointments`, reservar/reagendar/cancelar, exclusão de overlapping no banco |
| Attachments | Inexistente | P1 | Attachment, ScanResult | Upload/quarentena/metadados/download autorizado; limite e retenção |
| Comments | Inexistente | P1 | Comment, CommentRevision | Criar/editar/redigir; autoria e janela de edição |
| Timeline | Inexistente | P1 | Projeção de eventos autorizados | `/work-orders/{id}/timeline`; DTO de negócio, não dump de audit |
| Notifications | Inexistente | P1 | Notification, NotificationPreference | Caixa in-app, leitura e preferências; canais externos adiados |
| Audit | Parcial: escrita interna | P1 | WorkOrderAudit; AdministrativeAudit futuro | `/audit-events`, filtro/consulta privilegiada tenant-local |
| Search | Inexistente | P2 | Projeções PostgreSQL | `/search`, filtros por tipo e autorização antes do limite |
| Dashboards | Inexistente | P1 | Projeção agregada | `/dashboard/overview`, agregação limitada e benchmark |
| Reports | Inexistente | P2 | ReportJob, ReportArtifact | `/reports`, assíncrono com limite, cancelamento e download autorizado |
| Webhooks | Inexistente | P2 | WebhookEndpoint, Delivery, Outbox | `/webhooks`, entregas/retry; assinatura, SSRF e histórico |
| Integrations | Inexistente | P3 | Depende do parceiro concreto | Sem endpoint fictício; definir adaptador/contrato quando houver integração |
| Plans | Inexistente | P1 | PlanVersion, Entitlement | `/plans`, versões publicadas; administração em control plane |
| Subscription | Inexistente | P1 | Subscription | `/subscription`, lifecycle/trial/downgrade; sem pagamentos externos agora |
| Usage | Inexistente | P1 | UsageCounter, UsageLedger | `/subscription/usage`; medição por período, operação e tenant |
| Quotas | Inexistente | P1 | Entitlement, QuotaReservation | `/tenant/limits`; consumo atômico, proteção de vizinhos |
| Tenant Settings | Inexistente | P1 | TenantSettings | `/tenant/settings`; timezone/locale e limites de alteração |
| Platform Administration | Inexistente | P0 | PlatformOperator, ProvisioningJob, AdminAudit | `/platform/tenants`, provisionar/ativar/suspender; audiência/processo/grants separados |

**31 domínios planejados; 0 domínios com baseline produtivo completo.** WorkOrders tem núcleo funcional testado, mas não inclui toda a operação planejada. Cobertura de endpoints e testes está no catálogo. Nem linhas na matriz nem um controller equivalem a um domínio concluído.

Decisões: `/tenant` representa o contexto do domínio validado, evitando um segundo seletor de tenant; URLs de Platform Administration não concedem acesso ao conteúdo de clientes. Identidade não será duplicada com `/login` próprio. Rejeição/pausa/reabertura não serão implementadas antes de definir compensações, SLA e efeitos na agenda. Timeline/histórico usam uma única projeção; listagens por cliente/prestador reutilizam filtros autorizados, sem multiplicar rotas idênticas. UUIDv7 já é usado para novas ordens; preservar IDs existentes e FKs, medir antes de mudar estratégia. Paginação keyset padrão `limit=25`, máximo 100, cursor vinculado ao tenant/ator e filtros; listas pequenas de planos também têm limite explícito.
