# Verificações de segurança

Gate inicial: testes maliciosos do docs/testing/test-strategy.md + revisão de SQL/RLS/grants e permissões de recurso. Nenhum 2xx com dados alheios. Tests de filtro EF sozinhos não aprovam isolamento; executar SQL sem filtro com role runtime e tentar INSERT/UPDATE cruzado.

CI precisa de: analyzers/SAST; auditoria transitiva NuGet com falha se feed indisponível; secret scanning real (ex. Gitleaks, versão fixada); scan da imagem (ex. Trivy, quando imagem existir); DAST autenticado em Staging com contas de A/B e trajetórias completas. Ferramentas são candidatas até execução e evidência registradas. Regex local de segredos é verificação preliminar, não substitui scanner.

Exercitar replay/idempotência, payload extra, spoof de Host/forwarded headers, token inválido/expirado/issuer/audience, membro suspenso, alteração de permissão, rate abuse, query bound e falha de banco. UI: XSS/CSRF/redirects/session fixation assim que existir. Anexos: path traversal, MIME divergente, malware controlado, signed URL expirada/tenant errado. Cache/busca/SignalR: não aplicável até componente existir.

Registrar commit, ferramenta/versão, configuração sanitizada, cobertura, exclusões justificadas, resultados e correções. Críticos bloqueiam release. Não afirmar segurança concluída por compilar ou por um conjunto de testes negativos.

O [gate local](../testing/local-validation.md) já reúne checks reais com manifesto: auditoria NuGet JSON interpretada, Gitleaks fixado, testes e alinhamento de migrations. Ausência de ferramenta, diagnóstico do scanner e evidência incompleta são falhas. Não substitui SAST especializado, scan de imagem, DAST ou revisão de segurança; esses gates continuam pendentes.
