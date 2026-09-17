# Resposta a incidentes

Suspeita de breakout cross-tenant é incidente crítico: preservar request/trace IDs e timestamps sem copiar dados pessoais; restringir endpoint/tenant afetado quando necessário; interromper rollout; revogar credenciais comprometidas e preservar evidência com acesso limitado. Não apagar logs ou reparar dados sem snapshot e plano.

Papéis a nomear antes de produção: comandante do incidente, responsável técnico, comunicação e privacidade. Canais/on-call/escalonamento precisam ser configurados e testados; ainda não há equipe operacional designada. Comunicar usuários/autoridades conforme avaliação factual e obrigações aplicáveis, sem prazos legais presumidos neste documento.

Fluxo: detectar → classificar → conter → determinar janela/escopo → corrigir → validar isolamento/integridade → recuperar → monitorar → post-mortem com ações e responsáveis. Segredos não entram em ticket/chat. Banco comprometido exige restaurar em ambiente separado e verificar integridade; rollback de código não desfaz vazamento.

Simulado obrigatório antes da release: token exposto, tenant breakout e indisponibilidade do primary. Medir detecção, contenção e recuperação. Runbooks em docs/operations/runbooks; ausência de exercício mantém readiness bloqueado.
