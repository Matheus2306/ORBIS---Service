# ADR-017 — host HTTP de administração de memberships

2026-09-24. Decisão implementada; validação local no relatório de administração HTTP. Integração operacional com IdP e deployment não concluídos.

## Problema, contexto e requisitos

ADR-016 fornece alteração transacional de acesso, mas não permite usuários administrarem vínculos por HTTP. ADR-015 proíbe entregar credencial privilegiada à API comum. Exigir processo, audiência, credencial e configuração separados; MFA recente verificável; tenant por domínio/identidade/membership; contratos sem mass assignment; idempotência, concorrência e audit preservados.

## Alternativas e decisão

Ampliar o processo comum ou registrar dois datasources nele viola a fronteira de credenciais. Duplicar autenticação e infraestrutura HTTP favorece divergência de controles. Referenciar o assembly da API comum no host administrativo expõe risco de descoberta acidental de controllers. Adotados `Orbis.Hosting` para configuração compartilhada e `Orbis.Administration.Api` independente, sem referência entre hosts. Não há broker/cache/framework adicional; o único novo processo hospeda as operações privilegiadas existentes no monólito modular.

Compartilhados: validação JWT estrita, resolução HTTP do tenant, Problem Details, configuração Npgsql e algoritmo de readiness. Matriz de privilégios reside na infraestrutura e recebe um perfil explícito. Perfis se excluem: administração altera somente três colunas de memberships e insere audit/recibo, sem leitura/escrita de ordens; API comum não altera memberships nem lê audit administrativo. Credenciais trocadas falham no startup. Ambos verificam dez tabelas, seis com RLS forçada; nenhuma migration nova neste incremento.

## Autenticação e transporte

JWT de acesso assinado com uma única audiência administrativa; audiência configurada deve diferir da comum. Client IDs têm allowlist. `acr` único deve ser exatamente o nível configurado, cujo contrato com o IdP atesta MFA. `auth_time` único deve representar autenticação real com idade máxima configurável de 60–900s; `iat`/auth_time não podem ser futuros além da tolerância de 30s, nem auth_time posterior a iat além dela. Renovar token não renova MFA. Ausência/duplicação/inconsistência nega acesso. Roles/amr declaradas pelo cliente não substituem o contrato nem concedem ManageMembers.

O host exige HTTPS nas rotas `/v1`, não redireciona bearer em plaintext, não aceita ForwardedHeaders arbitrários e marca respostas `no-store`, inclusive erros. Kestrel encerra TLS neste desenho; terminação em proxy exige futuro desenho/teste de proxies confiáveis. DNS/ingress administrativos devem encaminhar para o processo correto e preservar domínio verificado: alias administrativo precisa de registro/verificação por tenant, nunca inferência pelo sufixo.

## Contratos, falhas e limites

PATCH de permissões e POST suspend/activate usam campos parciais: atributo ausente é preservado dentro da transação Serializable. Não fazer leitura HTTP seguida de substituição de um snapshot antigo. Fingerprint diferencia campo ausente e valor explícito, mantendo hashes anteriores de comandos completos. UUID Idempotency-Key + expectedVersion são obrigatórios. JSON estrito, campos desconhecidos/duplicados e nomes de permissões inválidos rejeitados. `reason` da rota candidata foi retirado: não existe requisito validado de texto livre de justificativa nem política de retenção/redação; auditoria continua registrando ator, alteração, versão e horário.

Body máximo8KiB; quatro operações simultâneas por instância, sem fila; pool máximo8, readiness máximo2. Orçamento cooperativo de request5s inclui autenticação, resolução e comando; backchannel JWT3s. Npgsql timeout de conexão/comando5s. Cancelamento é propagado, não garantia de abort instantâneo de rede/commit: resposta perdida deve repetir mesma chave. Erros temporários503; conflitos409; autorização de alvo404; claims insuficientes403; token inválido401. Não repetir automaticamente operações sem idempotência.

## Experimentos e consequências

Testes com PostgreSQL18 real e role restrita, tokens RSA reais e jornada Kestrel HTTPS, além de abuso de audiência/MFA/tenant, payload, replay, revogação, último administrador, grants e bloqueio real de linha. A suíte de ordens permanece necessária após extrair hosting. São testes funcionais, sem benchmark HTTP/p95/p99 ou ganho de capacidade alegado. Limites iniciais são proteção conservadora, não capacity planning validado.

Novo host exige implantação/configuração/segredos/monitoramento próprios e acrescenta até8 conexões por instância; orçamento total inclui 20 por instância comum, migrations e operação. Não aumentar max_connections sem medição. Sem infraestrutura contratada, custo monetário adicional não medido. Rollback desliga/reverte somente binário administrativo e preserva coluna/audit/recibos; nunca rebaixar schema com histórico.

## Riscos e reconsideração

Um ACR mal configurado pelo IdP pode afirmar MFA incorretamente; integração e revisão do provedor são gates operacionais. Os testes substituem discovery, não provam login/MFA real ou rotação de chaves. Domínios/primeiro administrador são provisionados por operação controlada ainda não entregue. Não existe operador global, convite ou escalonamento por role JWT. Readiness detecta drift com TTL5s; não é sandbox de SQL nem bloqueio instantâneo de instância. Proteção DDoS, quotas distribuídas, proteção contra replay de token roubado dentro da validade, retenção de audit e benchmark continuam pendentes. Reconsiderar limites por evidência; adicionar DPoP/mTLS somente com modelo de ameaça e suporte do IdP/clientes.

Fontes: [RFC9068 — perfil JWT de acesso](https://www.rfc-editor.org/rfc/rfc9068.html), [RFC9470 — step-up e auth_time](https://www.rfc-editor.org/rfc/rfc9470.html), [timeouts cooperativos ASP.NET Core10](https://learn.microsoft.com/en-us/aspnet/core/performance/timeouts?view=aspnetcore-10.0).
