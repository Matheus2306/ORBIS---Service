# Evidência de criação transacional

Data: 2026-09-17. Revisão: incremento deste relatório sobre `1382f4f`. Mesmo ambiente local do relatório authenticated-api: Windows/.NET 10/PostgreSQL 18.6, loopback/SCRAM/role restrita; build Release, fixture pequeno, sem Docker ou IdP operacional. Nenhuma dependência adicionada.

Resultado: **75/75 testes aprovados, zero falhas/ignorados** (24 domínio/arquitetura + 51 integração/HTTP). Build sem avisos/erros, format verify aprovado, modelo tenant alinhado à migration. Gitleaks 8.30.1 working tree sem leaks; dependências inalteradas desde auditoria NuGet registrada no incremento anterior.

Doze POSTs HTTP concorrentes com mesma chave devolveram o mesmo ID/data/Location: um criado e onze replays. Nova descrição com a chave original retornou 409. Cem comandos concorrentes contra banco real produziram um Created e 99 Replayed, com uma ordem/recibo/audit; não são 100 usuários simultâneos de produção. Chave pode ser reutilizada por outro ator ou tenant sem colisão de namespace. Vínculo revogado nega replay. Cliente não define tenant/customer no payload e domínio B não concede membership.

Auditoria e recibos protegidos por RLS ENABLE/FORCE, filtros, FKs e grants append-only. Testes SQL bruto cross-tenant retornaram 42501; ausência de contexto ocultou todos os dados. UPDATE/DELETE/TRUNCATE do histórico foram negados. Constraint de falha adicionada ao audit fez a criação lançar erro e deixou zero ordem/recibo da tentativa; removida a falha, mesma chave criou um único conjunto. Isso prova atomicidade local nos casos exercitados, não recuperação de falha de rede durante commit.

Execução: `./scripts/test-postgres.ps1`; artefatos `.artifacts/postgres/9aec8d75f5c24738b97569eb31a81196/test-results/`. SHA-256 unitário `1A98E66A0B780246B85748DB010B2647E6ACA33EE075E56F2024CB400AA55297`; integração `718E1994DC7E688F9B6855BC0B3E21190B10B43CE55A9ED3544B933DFB2EC7BC`. Cluster encerrado. Integração aproximadamente 11s, não benchmark de latência.

Limites: transições HTTP/listas/quotas/retention jobs não implementados; migração somente em fixture pequeno; nenhum teste distribuído, p95/p99/RPS, Small dataset, HA, restore ou capacidade 1M. Status de produção continua NO-GO.
