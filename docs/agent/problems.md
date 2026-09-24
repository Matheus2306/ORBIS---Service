# Problemas e dependências

| ID | Impacto | Estado / encaminhamento |
|---|---|---|
| ENV-01 | Docker indisponível | Integração local usará PostgreSQL instalado em cluster separado; imagens só serão validadas com Docker |
| ENV-02 | Shell sem saída de rede por padrão | Consulta oficial NuGet autorizada via revisão automática; restore requer mesma modalidade |
| OPS-01 | Sem cloud, domínio, IdP, cofre ou orçamento provisionados | Trabalho local continua; implantação, HA e custos reais permanecem não validados |
| PERF-01 | Nenhuma medição da aplicação | Priorizar baseline; proibir rótulo 1M SCALE VALIDATED |
| PERF-02 | Host de carga/telemetria de recursos ainda ausentes | Gerador externo preparado e transporte testado; implementar execução separada antes de medir capacidade |
| PROD-01 | Hipóteses comerciais ainda sem pesquisa com clientes | Entitlements propostos, sem cobrança ou promessa contratual |
| DEV-01 | Inicialização direta falha sem ConnectionStrings:Orbis | Ambiente local operacional precisa de conexão, migrations/grants e IdP; testes efêmeros não preparam execução direta |
| API-01 | Baseline funcional incompleto | Ordens/contexto/consulta e comandos administrativos em host separado implementados; IdP real/convites/provisionamento pendentes; consultar catálogo; API BASELINE READY pendente |
