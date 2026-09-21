# Evidência de métricas nativas e atributos controlados

Incremento sobre `efe8450`, 2026-09-21, Windows x64/.NET SDK 10.0.302/runtime 10.0.10/PostgreSQL 18.6. API Release em Performance, Kestrel HTTPS e VerifyFull no banco. Não foi instalado exportador, coletor ou biblioteca nova.

Problema: o nome padrão de pool Npgsql deriva da conexão. A API passa a usar nome constante `orbis-runtime`; isso contém metadados/cardinalidade e evita depender da sanitização da connection string pelo driver. O [comportamento é documentado pelo Npgsql](https://www.npgsql.org/doc/diagnostics/metrics.html). Limites de pool/timeouts/TLS/autorização permanecem exigidos.

Experimento: MeterListener captura cinco requests de negócio com resultados 200/201/400/401/404, emissão de duração HTTP/TLS/conexão, comandos DB e snapshots de max/ocupação do pool. Atributos HTTP/DB passam por allowlists; token, senha, usuário/banco da conexão, caminho de certificado, host/IDs do tenant e marcador enviado em body/query não aparecem. Falha de privacidade não imprime os valores. Instância HTTP filtrada por IMeterFactory; instrumentos Npgsql globais separados pelo pool nomeado.

O teste focado passou em `.artifacts/postgres/85e04265d56f4bc391fe62726019e4ff/test-results/`. Suíte completa: **119/119 aprovados, zero falhas/ignorados** (25 unitários/arquitetura + 94 integração). Build Release sem avisos/erros, formatação e Gitleaks aprovados. Auditoria NuGet transitiva em 2026-09-21 não reportou pacotes vulneráveis em nenhum dos oito projetos consultados.

TRX completo em `.artifacts/postgres/4f961487a6fb41a9924eeafef494318b/test-results/`: unitário `08_29_02`, SHA-256 `006D5B5DEAB1EE762C2BAC8DBE2968D849853CB40C61C4F5428C64BB5C468DB5`; integração `08_29_06`, SHA-256 `5BD6350924B92055FFF89DCD8A9EA72487C44335BB858F23C81C10363140E233`. Integração 1m25s, ambos os clusters encerrados; duração não é resultado de performance.

Não há baseline de carga ou percentis representativos; as amostras apenas provam emissão e propriedades de segurança nos fluxos testados. CPU/RAM/GC/threads da API, CPU/locks/I/O do servidor DB, gerador e backend/alertas ainda precisam de coleta operacional. Métricas de operação DB não são latência de endpoint. Produção permanece **NO-GO**, capacidade 1M **NÃO VALIDADA**.
