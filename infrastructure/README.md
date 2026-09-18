# Infraestrutura

Nenhum recurso cloud provisionado. Produção candidata: API redundante, PostgreSQL HA/PITR, ingress TLS, IdP, secrets e telemetria protegida. Selecionar destino com custo medido e então escrever IaC reproduzível. Compose local será incluído com schema e testes; não equivale a plataforma de produção. Development/Testing/Performance/Staging/Production devem usar identidades, bancos e destinos distintos.

`postgresql/runtime-grants.sql` concede o mínimo à role pré-criada `orbis_runtime` após migrations; é usado pelos testes reais. Pressupõe role nova sem privilégios herdados/perigosos; não é reconciliador que remove grants antigos. API verifica permissões perigosas no startup/readiness. Provisionamento descartável local está em `scripts/with-local-postgres.ps1`, compartilhado por testes e gerador. Isso não fornece HA, backup/PITR nem IaC de produção.
