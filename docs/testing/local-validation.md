# Gate local reproduzível

Execute `./scripts/validate-local.ps1` na estação de engenharia. Requer PowerShell 7, SDK de global.json, Git, PostgreSQL 18 e Gitleaks **8.30.1** previamente obtido da [release oficial](https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1) com checksum verificado. O scanner padrão está em `.artifacts/tools/gitleaks-8.30.1/gitleaks.exe`; `-GitleaksPath`, `-PgBin` e `-Port` permitem outros caminhos/porta. O script não baixa executáveis, não modifica configurações globais, não publica artefatos e não usa banco existente.

O gate executa, nesta ordem: autotestes das regras de evidência; restore locked com reavaliação e sem HTTP cache; restore das ferramentas fixadas; formatação; build Release com warnings como erros; suíte completa contra cluster descartável TLS; auditoria NuGet transitiva em JSON v1; Gitleaks redigido; comparação dos dois modelos EF com migrations; verificação do diff. Migrations são aplicadas e exercitadas pela suíte real; a comparação adicional detecta alterações de modelo sem migration.

Scanner ausente/versão diferente, saída nativa não zero, relatório ausente/inválido, diagnóstico NuGet, qualquer dependência vulnerável, projeto omitido, teste falho/ignorado ou mudança do código durante a execução impedem aprovação. A listagem NuGet é analisada como JSON: exit code zero sozinho não comprova ausência de vulnerabilidades. Fonte e escopo são conferidos contra NuGet.Config e todos os projetos da solução. Não há flag para ignorar gates.

## Evidências

Cada execução cria `.artifacts/validation/<uuid>/manifest.json`, logs próprios de cada check e TRX novos. O manifesto contém UTC inicial/final, HEAD, dirty state, hashes de todos os arquivos fonte versionados/não ignorados, hash do scanner, duração/exit code/hash dos logs e contagens/hashes dos testes. O snapshot de arquivos é comparado novamente ao final: não editar o projeto enquanto o gate executa. HEAD com dirty=true identifica um incremento ainda não commitado, não uma execução do commit puro.

Falha conserva manifesto e logs locais. O cluster PostgreSQL continua em `.artifacts/postgres/<uuid>`, encerrado pelo mesmo finally já usado na integração; diretórios não são apagados automaticamente. Credenciais não são argumentos dos comandos nem campos do manifesto. Logs/TRX podem conter contexto sintético e erros internos: revisar antes de compartilhar. O scanner de fontes exclui `.artifacts`, `.git`, bin e obj conforme `.gitleaks.toml`; isso não aprova o conteúdo excluído para publicação.

As regras possuem casos sintéticos que rejeitam evidência incompleta, feed com diagnóstico, vulnerabilidades diretas/transitivas, contagem inconsistente, teste ignorado e assembly errado. Esses casos são autotestes do gate, separados dos testes da aplicação; não contam como usuários/carga.

## Alcance da aprovação

`status=passed` significa somente `scope=local-engineering`; `productionApproved` permanece false. CI remoto, imagem/scan de container, DAST autenticado, baseline/carga, backup/restore e deploy/rollback continuam pendentes. Analyzers de build não equivalem a revisão de segurança completa. O gate não mede capacidade, não garante atualização do feed além da resposta obtida e não certifica SLO.

Migração para CI deverá preservar os validadores e executar contra snapshot limpo, armazenar evidências com acesso restrito e incluir os gates pendentes. Não marcar F6 concluído por possuir um script local. Não usar um relatório antigo para promover uma revisão diferente.

[Execução real e hashes](reports/2026-09-21-local-gate.md): 119 testes de aplicação e 21 casos das regras; gate local aprovado, produção não aprovada. Somente o caminho Windows foi executado.
