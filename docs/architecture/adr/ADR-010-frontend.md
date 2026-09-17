# ADR-010 — interface independente da API

Status: candidato, validar com fluxo completo antes de adoção final. Problema: três experiências mobile com identidade visual do tenant. Requisitos: acessibilidade, feedback, performance, manutenção e capacidade sem sessões persistentes por usuário no servidor.

Alternativas: Blazor Web App SSR simplifica C# e primeira renderização; Interactive Server mantém circuitos e exige capacity planning por conexão; WebAssembly desloca execução, aumentando payload inicial; React/TypeScript possui ecossistema amplo, mas segunda stack e BFF. Decisão candidata: Blazor Web App SSR com interatividade seletiva WebAssembly, API separada. Não usar Interactive Server globalmente para o pico de 1M sem ensaio. Comparar tamanho transferido, LCP/INP, acessibilidade e esforço no primeiro fluxo contra React quando necessário.

Benchmarks: nenhum; não chamar escolha de superior empiricamente. Riscos: autenticação browser/API e duplicação de DTOs, prerender, reconexão e download inicial. Design system: escala de espaçamento 4/8/12/16/24/32, foco visível, texto legível, contraste AA, labels persistentes, erros próximos ao campo, carregamento/empty/retry, agenda útil no mobile e navegação por persona. Não expor detalhes de infraestrutura ao usuário.

Reconsiderar se acessibilidade, startup ou experiência offline demandarem solução alternativa. Fonte: [modelos de hospedagem Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0).
