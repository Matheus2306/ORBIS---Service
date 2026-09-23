# ADR-016 — alterações transacionais de acesso ao tenant

2026-09-23. Status: implementado no domínio/aplicação/persistência. Exposição HTTP depende do host administrativo do ADR-015.

## Problema e requisitos

Dois administradores podem retirar seus próprios privilégios simultaneamente e deixar o tenant sem administrador. Uma versão por membership evita perda de atualização da mesma linha, mas não protege esse predicado entre linhas. Repetições, revogações, delegação limitada e auditoria também precisam de resultados definidos. A API comum permanece SELECT-only em memberships; nenhuma concessão administrativa é automática.

## Alternativas e decisão

Read Committed + COUNT/UPDATE não protege write skew entre administradores distintos. Advisory lock por tenant exige protocolo cooperativo/chave e serializa todas as alterações; linha coordenadora com FOR UPDATE acrescenta estado/acesso e também serializa operações independentes. São alternativas futuras, não experimentadas.

Selecionado Serializable PostgreSQL (SSI), versão esperada e retry limitado da transação inteira. Já disponível na dependência usada, detecta conflitos de leitura/escrita sem serviço adicional. Escolha de correção, sem alegação de superioridade de desempenho. O domínio exige ManageMembers=256, ator ativo e todas as permissões atuais e desejadas do alvo contidas nas do ator. Suspender ou retirar acesso superior ao seu também é proibido. ManageMembers não implica ReadMembers.

## Contrato adotado

Cada comando abre transação Serializable tenant-scoped com SET LOCAL/RLS, relê estado global de tenant/ator, membership, alvo e delegação antes do recibo. Ativação exige conta global ativa. Remover ManageMembers ou suspender administrador exige outro vínculo ativo com ManageMembers e conta global ativa. O predicado participa da SSI. Versão inicial 1, incremento por alteração efetiva; comando sem efeito retorna conflito.

`membership_access_changes` guarda antes/depois, versões, ator, membro, instante, chave e fingerprint. PK tenant/ator/chave; unicidade tenant/membro/versão. Um registro append-only serve como auditoria e recibo, no mesmo commit da alteração. FKs compostas, RLS forçada, proteção EF e grants mínimos. Retenção deve atender aos dois contratos; não expirar recibos apagando auditoria.

Chave igual/conteúdo igual retorna resposta original após reautorização; conteúdo divergente ou versão vencida retorna conflito. Serialização/deadlock e colisão da PK de recibos permitem até três tentativas completas com jitter curto. Esgotamento retorna Busy. Falhas de rede/timeout ou commit de resultado desconhecido não têm retry cego; cliente futuro repete a mesma chave. Cancelamento é respeitado.

Revogação confirmada antes do snapshot nega o comando. Transações iniciadas podem ser serializadas antes da revogação e terminar; não há cancelamento de trabalho em voo. Futuros comandos globais de suspensão de usuário/tenant e provisionamento de administradores devem participar da política de concorrência e ganhar testes próprios. Esta proteção não controla SQL operacional arbitrário nem garante disponibilidade do IdP/bootstrap.

## Evidência e benchmarks

Barreira força snapshots concorrentes de dois administradores para suspensão e retirada de ManageMembers: um efeito, um conflito, um administrador restante e retry observado. Cem comandos iguais geram um efeito; cem chaves distintas com a mesma versão também. Chave compartilhada entre alvos distintos preserva um fingerprint. Constraint real no histórico prova rollback. Testes abrangem RLS, grants, revogação e orçamento/cancelamento de retries. Injeção de 40001 mede só limite de retries; conflitos reais são exercitados separadamente.

[Relatório](../../testing/reports/2026-09-23-membership-administration-core.md). Esses ensaios são de integridade, não benchmarks de capacidade. RPS, percentis HTTP, overhead SSI, noisy neighbor e ponto de ruptura permanecem não medidos.

## Consequências e riscos

Credencial `orbis_membership_admin`: SELECT do diretório/memberships, UPDATE apenas permissions/is_active/version, SELECT/INSERT do histórico. Sem criação/exclusão de membros, mutação de IDs, ordens, DDL ou bypass RLS. Script operacional não cria login/senha nem revoga grants extras antigos. Somente o harness local cria credencial aleatória efêmera. Host/guard administrativos, audiência distinta/MFA continuam pendentes; serviços administrativos não foram registrados na API comum.

Migration aditiva acrescenta versão, histórico e bit256 sem concessão. Lock/statement timeouts limitados; Down recusa histórico, versão diferente de 1 ou bit administrativo. Aplicar schema antes do novo binário; rollback preferencial de binário preserva schema/dados. API comum anterior pode continuar lendo colunas/flags conhecidas no schema expandido. Small não valida zero downtime em volume maior.

Receita sintética v2 incorpora versão/histórico nos checksums; IDs/hashes mudam em relação à v1, cujas evidências permanecem históricas. Receita de performance não concede administração. Pool administrativo precisa entrar no orçamento conjunto de conexões de produção.

## Reconsideração

Reavaliar SSI se carga real mostrar aborts/Busy, p99 ou impacto entre tenants excessivos. Medir antes de adicionar coordenação/índices. Antes da exposição pública, testar processo/credenciais, grants efetivos administrativos, MFA, DTOs/erros/quotas e HTTP. Nenhuma nova dependência foi introduzida.

Fontes primárias: [isolamento e SSI](https://www.postgresql.org/docs/18/transaction-iso.html), [consistência de regras entre linhas](https://www.postgresql.org/docs/18/applevel-consistency.html), [políticas RLS](https://www.postgresql.org/docs/18/ddl-rowsecurity.html).
