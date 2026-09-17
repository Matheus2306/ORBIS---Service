# ADR-009 — fronteira pública e hospedagem

Status: requisitos definidos; fornecedor e implantação pendentes. Problema: TLS, tráfego hostil, domínio e distribuição de assets. Requisitos: 99,95%, entrada autenticada, mitigação DDoS, rotação de certificados e recuperação.

Alternativas: serviço gerido de containers + LB, VMs com automação, Kubernetes; edge do provedor ou Cloudflare. Decisão candidata: containers em plataforma gerida, pelo menos duas instâncias em zonas distintas para API, banco HA gerido com PITR. Kubernetes não justificado por um serviço. Cloudflare pode agregar DNS/WAF/CDN, mas precisa de experimento de latência/custo/compatibilidade e procedimento para falha do fornecedor. Nenhum serviço contratado.

Evidência: SLO solicitado exige eliminar falhas únicas; não comprova disponibilidade real. Rate limiter .NET é por processo, não proteção DDoS ou quota global; limite total cresce com réplicas. TLS termina em proxy conhecido e conexão interna protegida; forwarded headers limitados a proxies confiáveis; origem restrita ao ingress.

Consequências: custo mínimo de redundância e dependência do provedor. Riscos: DNS/TLS, origem exposta, limites incorretos e failover não testado. Reconsiderar orquestração com serviços/equipe/autoscaling medidos. IaC será específico do destino; compose local não é IaC de produção validada. Fonte: [ASP.NET rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).
