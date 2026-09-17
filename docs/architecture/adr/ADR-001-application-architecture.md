# ADR-001 — monólito modular

Status: adotado como arquitetura inicial em 2026-09-17; desempenho ainda não medido.

Problema/contexto: núcleo relacional com workflow transacional, API independente e equipe operacional ainda não constituída. Requisitos: isolamento, evolução, stateless HTTP, recuperabilidade e metas de carga explícitas.

Alternativas: camadas sem limites de domínio são simples mas favorecem acoplamento; vertical slices reduzem navegação por caso de uso; clean architecture integral pode gerar indireção excessiva; microservices introduzem rede e consistência distribuída sem gargalo identificado. CQRS lógico para separar consultas/comandos é útil, sem bancos separados ou event sourcing.

Decisão: módulos com domínio puro, aplicação, infraestrutura e API; slices por caso de uso dentro dessas fronteiras. Um deploy de API; worker separado somente quando jobs duráveis existirem. EF funciona como unidade transacional, sem repository genérico. Domínio não conhece ASP.NET ou EF. Referências entre módulos por contratos internos pequenos.

Justificativa: transações locais protegem invariantes com menor custo operacional. Benchmarks: nenhum comparativo disponível, decisão organizacional/funcional. Consequências: release compartilhada e risco de noisy neighbor no processo; limites de concorrência e medições obrigatórios. Risco de monólito acoplado mitigado por testes de dependência. Reconsiderar extração quando perfis de escala, ownership ou disponibilidade divergirem e medição mostrar ganho líquido.
