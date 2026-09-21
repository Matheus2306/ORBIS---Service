# Transporte HTTPS real da API

2026-09-21, incremento sobre `2af1929`. Windows x64, SDK 10.0.302/runtime ASP.NET 10.0.10 e PostgreSQL 18.6. API em Performance, build Release, Kestrel HTTPS restrito a loopback/porta dinâmica e PostgreSQL com VerifyFull/SCRAM/runtime sem bypass de RLS. Nenhuma mudança de contrato, schema, dependência ou configuração de produção.

## Experimento e correção

Em 2026-09-18 os quatro casos novos falharam com EOF no handshake; os 114 anteriores passaram. Logs temporários do Kestrel confirmaram AuthenticationException por chave efêmera não suportada pela plataforma, Win32 0x8009030E. Diagnóstico em `.artifacts/postgres/6654cbfa70514f5c8a15208924fddea4/test-results/`, sem alteração das expectativas para ocultar a falha.

Correção limitada ao harness: no Windows, exportar PEM para PKCS#12 somente em memória e importar via X509CertificateLoader/UserKeySet sem PersistKeySet. O certificado é liberado após o servidor, e o buffer temporário é zerado. Não instalar CA nem aceitar certificados inválidos. A limitação coincide com a [issue do runtime](https://github.com/dotnet/runtime/issues/23749); a importação usa a [API oficial](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificateloader.loadpkcs12?view=net-10.0). Logs Debug temporários foram removidos após o diagnóstico.

O certificado de teste agora aceita IP 127.0.0.1 e hosts `*.orbis.test`. O destino de rede permanece loopback; o Host usado na resolução do tenant também pode ser empregado pelo HttpClient como SNI. Nenhuma alteração de DNS/arquivo hosts. O cliente confia exclusivamente na CA do run; revogação é NoCheck porque essa CA descartável não publica CRL/OCSP. Não extrapolar isso para produção.

## Casos executados

Para cada versão exata HTTP/1.1 e HTTP/2: acesso anônimo recebe 401; JWT válido recebe 200; criação e replay recebem 201 com mesmo recibo e flags false/true; ID de tenant alheio e troca de Host sem membership recebem 404; assinatura JWT incorreta recebe 401. Dois testes separados exigem AuthenticationException com raiz alheia ou nome localhost ausente do SAN. Discovery OIDC é estático no fixture; validação criptográfica continua real. Não foi integrado um IdP operacional.

A reprodução focada após correção passou 4/4 casos, zero ignorados, em `.artifacts/postgres/06dbf38313414585853447ac192541ea/test-results/`. A suíte completa passou **118/118 testes, zero falhas/ignorados** (25 unitários/arquitetura + 93 integração). Build Release sem avisos/erros, formatação e Gitleaks aprovados. Dependências não alteradas neste incremento.

TRX da suíte completa em `.artifacts/postgres/e0d9a546488d4f67b570e144993cb6b4/test-results/`: unitário `08_12_52`, SHA-256 `33BB395980972AAA86858D7D48B53A2BDADC94C06A0CC2246473ACD43A0E76A2`; integração `08_12_58`, SHA-256 `0F38768E38282E4DCD3FB377F029678F1590E6AA1F0A258B41351C660F1DB0BF`. Integração durou 2m1s; ambos os clusters foram encerrados. Duração de suíte não é benchmark.

## Limites

São testes funcionais com fixtures pequenos, cliente e servidor no mesmo processo/máquina. Não há RPS, p95/p99 HTTP, carga distribuída, proxy, IdP externo, revogação, HA ou restore validados. Nenhuma capacidade foi extrapolada. **Produção NO-GO; 1M NÃO VALIDADO.** Próximo incremento: sinais nativos de HTTP/runtime/pool, proteção de atributos de telemetria e instrumentação do gerador antes do baseline.
