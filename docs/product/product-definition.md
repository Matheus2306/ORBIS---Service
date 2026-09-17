# Definição do produto — v0.1

## Problema e valor

Organizações de serviços coordenam pedido, triagem, agenda e evidências em canais separados. ORBIS reúne o ciclo e torna estado, responsável e prazo visíveis para cliente e executor. Mercado inicial proposto: empresas de manutenção e assistência de pequeno e médio porte; expansão para outros segmentos sem codificar regras setoriais no núcleo. Hipótese comercial a validar com entrevistas; não existe pesquisa de mercado concluída.

## Personas e jornadas

| Persona | Necessidade | Jornada e critério de sucesso |
|---|---|---|
| Gestor do tenant | Controle e responsabilidade | Configura unidade/equipe/catálogo, convida membros, acompanha atrasos; só enxerga sua organização |
| Operador | Converter pedido em execução | Triagem, proposta quando necessária, designação e agenda; conflitos explícitos e auditados |
| Cliente | Resolver problema e acompanhar prazo | Entra pelo domínio da empresa, solicita, acompanha, aprova e avalia; vê apenas seus recursos |
| Prestador | Executar trabalho autorizado | Recebe atribuição, aceita, inicia, registra evidência e conclui; escopo por membership |
| Operador da plataforma | Manter SaaS | Provisiona e suspende tenants por trilha privilegiada, sem leitura ordinária do conteúdo dos clientes |

Conta global não concede acesso automático a todos os tenants. Um usuário pode exercer várias personas em diferentes organizações. Perfil público de prestador compartilhável no futuro apenas por consentimento; histórico e avaliações contratuais pertencem ao tenant.

## Núcleo de domínio

- **Tenant management:** organização, domínio verificado, unidade, ciclo de vida e assinatura.
- **Identity & access:** usuário global, identidade externa (issuer + subject), membership, papéis traduzidos em permissões.
- **Service operations:** catálogo, solicitação, proposta, agendamento e ordem. Solicitação expressa demanda; ordem é compromisso de execução. Relação 1:N futura; primeiro incremento 1:1 explícito.
- **Providers:** competências e disponibilidade locais ao tenant; designação exige vínculo ativo.
- **Documents, notifications, reporting:** módulos posteriores; nenhuma autorização derivada apenas de URL ou ID.

Vocabulário: cliente solicitante, técnico executor, operador despachante, unidade local, prazo de SLA, janela de atendimento. Datas em UTC, fuso do tenant para apresentação; moeda explícita e valores decimais quando preço entrar no escopo.

## Fluxo de produção inicial

Tenant ativo → membership ativo → catálogo → solicitação → triagem → atribuição → aceite → início → conclusão → avaliação. Cancelamento e reagendamento são transições autorizadas, nunca edição arbitrária de status. Primeiro corte de código protege ordem e acesso; catálogo, proposta e agenda não serão simulados por campos livres para alegar conclusão do produto.

Estados candidatos da ordem: Requested → Accepted → InProgress → Completed; Requested/Accepted → Cancelled. Só o executor designado inicia/conclui; gestor autoriza atribuição; cliente cancela antes do início conforme política. Mudanças concorrentes usam versão; repetição é idempotente no comando, não duplicação de eventos.

## SaaS e limites (hipóteses de produto)

Planos Essential, Business e Enterprise; sem preços comerciais definidos. Entitlements versionados, não condicionais espalhadas por nome do plano. Dimensões: membros internos ativos, unidades, ordens/mês, armazenamento e integrações. Limites técnicos de proteção são diferentes de quotas comerciais. Clientes finais não consomem assento interno. Tenant trial possui expiração explícita. Upgrade concede direitos após confirmação; downgrade entra no próximo ciclo e não apaga dados excedentes. Cancelamento agenda encerramento; suspensão bloqueia mutações, permite exportação autorizada em janela definida. Retenção e exclusão definitiva exigem política contratual antes de produção. Cobrança fora do primeiro incremento.

## Menor release de produção e roadmap

P0: isolamento, identidade, memberships, auditoria e ordens; P1: catálogo, clientes, prestadores, agenda e três portais; P2: anexos seguros, notificações confiáveis, SLA e observabilidade; P3: backup/restore, deploy/rollback e testes de carga/segurança; P4: relatórios, integrações e expansão de escala. A primeira release comercial depende de P0–P3 e dos gates operacionais. Não é descartável, tampouco será anunciada pronta enquanto tais itens faltarem.

Aceite transversal: autorização negativa testada, recuperação de falhas visível, acessibilidade por teclado, mobile, limites de consulta, registro auditável, SLO mensurável e runbook quando necessário.
