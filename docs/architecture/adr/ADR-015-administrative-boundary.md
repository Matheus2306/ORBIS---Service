# ADR-015 — fronteira de administração e privilégios efetivos

2026-09-23. Status: contrato de isolamento adotado; guard do runtime implementado neste incremento. Host/credencial administrativos e comandos ainda planejados, sem endpoints vazios.

## Problema, contexto e requisitos

Convites, provisionamento e alteração de memberships precisam de escrita hoje deliberadamente ausente da API comum. Compartilhar a credencial administrativa com esse processo ampliaria seu impacto em caso de comprometimento. Além disso, o guard anterior consultava grants de tabela e atributos da role atual: dois testes PostgreSQL demonstraram que UPDATE por coluna e uma cadeia de SET ROLE sem INHERIT escapavam da verificação de startup. Nenhuma SQL injection nem exploração HTTP foi demonstrada; a precondição é configuração inadequada de privilégios.

Preservar RLS, autorização por vínculo/recurso, identidade global, revogação e grants mínimos. Administração não pode ser alcançada por uma role de banco da API comum. Credencial operacional de migration nunca entra nos hosts. Nenhum grant automático a clientes/prestadores ou operador global por conta de um papel no JWT.

## Alternativas

1. Ampliar grants da API comum: rejeitado, desfaz a boundary já estabelecida.
2. Dois datasources no mesmo processo: facilita entrega, mas deixa a credencial privilegiada no processo exposto às jornadas comuns; rejeitado para esta boundary.
3. Funções SECURITY DEFINER: poderiam restringir comandos, mas acrescentam ownership/search_path/EXECUTE público e lógica privilegiada em SQL. Não adotadas sem necessidade e validação específicas.
4. Host administrativo separado, mesma solução/stack e banco, credencial específica: selecionado para os comandos futuros. Resolve isolamento de credencial; não é particionamento do domínio em microservices nem garante segurança por si só.

## Decisão e justificativa

Runtime comum usa role de propósito único, sem membership em outra role, mesmo com SET/INHERIT desabilitados. Isso impede cadeias transitivas e caminhos por ADMIN OPTION sem manter uma lista frágil de roles perigosas. current_user deve corresponder ao session_user. Roles superuser/BYPASSRLS/CREATEDB/CREATEROLE/REPLICATION, ownership nos schemas do produto e CREATE nesses schemas são rejeitados.

Matriz explícita das nove tabelas exige SELECT; work_orders admite INSERT/UPDATE; audit/receipts admitem apenas INSERT além de leitura; memberships/diretório permanecem somente leitura. Grants por coluna também são verificados. DELETE/TRUNCATE/REFERENCES/TRIGGER/MAINTAIN e delegação WITH GRANT OPTION são rejeitados. Cinco tabelas tenant-scoped continuam exigindo RLS forçada. Falta de um grant necessário também deixa startup/readiness não saudáveis. Nenhum grant existente foi ampliado.

Próximo incremento administrativo deve ter processo/configuração/credencial separados, audiência administrativa distinta e contrato de MFA verificável com o IdP. Tenant Admin continua sujeito a membership, tenant e permissão persistida; Platform Admin exige relação global explícita e não herda acesso aos dados do tenant. Não reutilizar token da API comum como credencial administrativa. Bootstrap de operador pertence à operação controlada, não a um endpoint público sem autenticação.

Antes de expor comandos: definir delegação limitada, último administrador ativo, audit transacional, versão esperada, idempotência com reautorização e consistência sob revogações concorrentes. Escolha de locks/isolation level depende de testes de concorrência; não assumir que um COUNT seguido de UPDATE protege o último administrador. Leituras existentes não concedem essas capacidades.

## Experimentos, medição e consequências

[Relatório de validação](../../testing/reports/2026-09-23-runtime-privilege-boundary.md): reprodução anterior, grant por coluna efetivamente permitindo UPDATE (revertido), SET LOCAL ROLE transitivo executado e guard antigo aceitando startup. Testes novos exigem rejeição e recuperação após remover a configuração indevida. Não é benchmark HTTP; nenhum aumento de capacidade ou custo de produção foi medido. O probe continua com timeout3s, TTL5s e execução única compartilhada, fora de cada consulta de negócio.

Uma implantação que dependesse de role de grupo, permissões por coluna em vez dos grants de tabela esperados ou privilégios extras passará a falhar explicitamente. O script de grants existente e os testes usam privilégios diretos compatíveis. Comparar configuração real antes de rollout; não desabilitar o guard para adaptar a produção. Host adicional terá custo operacional/deployment próprio ainda não estimado nem contratado.

## Riscos e reconsideração

Readiness detecta drift após seu TTL; não revoga privilégios nem interrompe requests já em execução. A infraestrutura precisa retirar instâncias unhealthy e o operador precisa corrigir grants. Esse guard não é sandbox para SQL arbitrário nem auditoria de todo o cluster: funções SECURITY DEFINER, policies permissivas novas, grants em outros bancos/schemas e configurações do provedor exigem revisão própria. A matriz precisa acompanhar novas tabelas/operadores.

Reconsiderar proibição de qualquer membership somente se um provedor exigir grupos de autenticação e houver allowlist de capacidades efetivas, testes de SET/INHERIT/ADMIN e revisão de risco. Reconsiderar host separado ou funções privilegiadas após observar custo/operabilidade e preservar isolamento equivalente. Fontes: [privilégios por tabela/coluna/role](https://www.postgresql.org/docs/18/functions-info.html), [semântica de memberships PostgreSQL](https://www.postgresql.org/docs/18/role-membership.html).
