# Evidência do gerador sintético

Data: 2026-09-18. Incremento sobre `924801f`. Windows x64, SDK 10.0.302/runtime 10.0.10, EF 10.0.12/Npgsql EF 10.0.3, PostgreSQL 18.6. Release build: zero warnings/erros. **108/108 testes aprovados, zero falhas/ignorados** (25 unitários/arquitetura/cursor + 83 integração). Formatação, Gitleaks working tree e auditoria NuGet transitiva sem achados reportados.

Dois imports independentes de cada perfil Small, Uniform e HotTenant, produziram contagens e hashes idênticos em todas as nove tabelas. Cada dataset possui 1.000 usuários, 50 tenants, 1.050 memberships e 10.000 ordens; o tenant concentrado recebe exatamente 3.000. Históricos e recibos correspondem às versões. Criação e conclusão executadas pelo código de aplicação sobre dados importados retornam Replayed. Runtime sob RLS não lê ordem/auditoria de outro tenant.

Uma constraint rejeitando conclusão durante COPY comprova rollback de usuários/tenants/ordens/auditoria/recibos já importados na mesma transação. Reexecutar contra dataset preenchido falha sem alterar 10.000 ordens existentes. Tabela desconhecida e seu registro são preservados, sem criar schema de diretório. Hosts externos, localhost não explícito, nome sem marca de teste e nome inválido são rejeitados antes de abrir conexão. Teste de receita confirma estabilidade entre culturas pt-BR/ar-SA, UUIDv7, 24 meses e variação por seed.

O importador usa role privilegiada de preparo, enquanto consultas e replay são testados com runtime restrito. Não desabilita RLS/constraints. Grants mínimos extraídos para arquivo operacional único, consumido pelo fixture. Script de cluster compartilhado executado tanto pela suíte quanto pela CLI; encerra servidor em finally e restaura ambiente.

TRX locais: `.artifacts/postgres/4b1157629160490fb5c5d266f1d71f03/test-results/`. SHA-256 unitário `F33CF939035FA65DEC0816203ABDDEAF6E6879ED025DCEFE296FDF334AB11122`; integração `8A3EAE9249AFA301196139A3E9609FC07AE85A373606A106CFC966A6183A2B05`. Integração durou aproximadamente 84s, incluindo múltiplas bases/importações; não é benchmark da API.

Limites: Medium/Large possuem receita mas não foram executados; não há validação de cancelamento por sinal do SO, falta de disco ou interrupção no instante do commit. Importação é transação exclusiva de preparo, sem suporte a carga online/resume. RPS, p50/p95/p99, saturação, noisy neighbor e 1M permanecem NÃO VALIDADOS. Manifestos Small e dimensões estão no relatório de preparação em `docs/performance/reports/2026-09-18-small-dataset.md`.
