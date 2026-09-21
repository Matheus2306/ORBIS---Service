# Backlog executável

Estados: **em curso**, **pendente**, **bloqueado**, **concluído com evidência**. Priorizar integridade antes de volume de telas.

| ID | Prioridade | Entrega / dependência | Gate verificável | Estado |
|---|---|---|---|---|
| F0 | P0 | Discovery, ADRs, ameaças, workload e SLO | Documentos e hipóteses explícitas | concluído com evidência: docs iniciais; revisão do threat model acompanha implementação |
| F1 | P0 | Solução .NET e domínio de acesso/ordens | Release build + invariantes + architecture tests | concluído com evidência: 13 testes, build Release |
| F2 | P0 | EF, migrations, tenant filters, RLS e FK composta | Banco real, role restrita, ataques cross-tenant e pool reuse | concluído com evidência: relatório de 24 testes; não abrange HTTP de negócio |
| F3 | P0 | API autenticada, memberships e resolução por host | Assinatura/issuer/audience + membership + recurso + contratos HTTP | concluído com evidência local: 54 testes; IdP operacional em P1 |
| F4 | P0 | Criação/aceite/conclusão e auditoria | Idempotência, 100 atualizações concorrentes, atomicidade | concluído no backend: relatório de 98 testes; performance em F5, UX em P3 |
| F3b | P0 | Listagem de ordens com keyset | Autorização no SQL, limite/cursor, contrato e isolamento | concluído funcionalmente: relatório de 86 testes; performance em F5 |
| F5 | P0 | Dataset Small e baseline Release | RPS/percentis/erros + métricas do gerador e servidor | em curso: Small, 960 amostras SQL sob RLS, HTTPS e emissão de métricas; 119 testes; carga HTTP pendente |
| F6 | P0 | CI reproduzível | Restore locked, format, build, testes, scanners e perf smoke | pendente |
| P1 | P1 | IdP real e onboarding do tenant | Convite/revogação/MFA administrativo; nenhuma credencial de demonstração | pendente |
| P2 | P1 | Catálogo, clientes, prestadores e agenda | Jornada completa + políticas de horário/concorrência | pendente |
| P3 | P1 | Portais administrativo/cliente/prestador | E2E, acessibilidade, mobile, estados vazios/erro/loading | pendente |
| P4 | P1 | Anexos e notificações | Quarentena, ACL, retenção, retry, replay e recuperação | pendente |
| O1 | P0 para release | HA, IaC, segredos, observabilidade e alertas | Deploy/rollback + indisponibilidade controlada | bloqueado: destino não provisionado |
| O2 | P0 para release | Backup/PITR/restore | RPO ≤5 min, RTO ≤30 min medidos | pendente |
| O3 | P0 para release | Security review e readiness | Sem críticos conhecidos; scans reais anexados | pendente |
| S1 | P1 | Medium + hot tenant/noisy neighbor | Limite por tenant e knee point medidos | pendente |
| S2 | P1 | Stress, spike, soak 1/4/8/24h, failover | Integridade pós-falha; gerador sem saturação | pendente |
| S3 | P1 | Large e carga distribuída representativa | Nível 6, 1M na janela definida, relatório auditável | bloqueado: infraestrutura equivalente |

O estado do agente informa o incremento corrente. Não fechar F3 com apenas autenticação falsa de testes nem O2 com apenas existência de script de backup.
