# Evidência de listagem autorizada

Data: 2026-09-17. Incremento sobre `8aac55e`. Windows x64, SDK 10.0.302, runtime 10.0.10, EF 10.0.12/Npgsql 10.0.3, PostgreSQL 18.6 real em cluster loopback exclusivo/SCRAM. Mesmos limites do relatório anterior; nenhuma dependência, migration ou índice adicionado.

**86/86 testes aprovados, zero falhas/ignorados**: 25 domínio/arquitetura/cursor + 61 integração/HTTP. Build Release sem avisos/erros; formatação limpa; Gitleaks working tree sem leaks. Dependências/modelo de banco inalterados desde gates registrados no incremento de criação. Integração aproximadamente 15s, sem significado de benchmark.

Casos novos: percurso de páginas com timestamps empatados, correspondência exata ao conjunto esperado sem duplicatas; inserção mais recente após a primeira página não desloca a continuação; cursor de outro tenant/ator retorna 404; adulterar ator no cursor não herda permissão ampla; prestador vê somente sua ordem atribuída e perde acesso após mudança de permissão; limites e cursores inválidos rejeitados; tamanho, versão e faixa temporal do cursor verificados. Predicado SQL único de leitura usado também no detalhe; testes anteriores continuam verdes.

Artefatos: `.artifacts/postgres/adc74e9c7f2c4bf698e155eeac4a9054/test-results/`. SHA-256 unitário `B834086676BEF8F50977250F5F97C928FF2687CCF96F322C86DF418F88E66065`; integração `0F0DCAFCBE1FB7C82E5CEDBA32147C6916297E50D68C09DBD9E11775871CC76C`. Cluster encerrado pelo script.

Não medidos: custo de busca por cliente/prestador em tenant grande, query plans, offset versus keyset, RPS/p95/p99, Small/Medium/Large, picos/soak ou 1M. Limite de 100 itens não é prova de consulta rápida; seletividade e índices serão avaliados com dataset representativo. Status NO-GO de produção preservado.
