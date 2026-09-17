# ADR-007 — anexos privados

Status: decisão de fronteira adotada; backend pendente de implantação de anexos. Problema: evidências e documentos potencialmente hostis/privados. Requisitos: ACL por tenant/recurso, backup, retenção, limites e quarentena.

Alternativas: filesystem simples local porém impede réplicas sem volume compartilhado; bytea aumenta backup/WAL do banco; S3/Azure Blob/R2 geridos separam custo/escala; MinIO adiciona operação. Decisão: metadados relacionais + objeto privado; filesystem apenas no adaptador Development. Escolha de serviço depende do destino e custos reais. Não adicionar SDK ou endpoint de upload agora.

Fluxo exigido: autorização → quota/tamanho → chave gerada pelo servidor → quarentena → validação MIME/conteúdo e malware → liberação → download autorizado com URL curta ou streaming. Nome original nunca vira caminho. Signed URL é credencial temporária; não registrar. SVG/HTML ativos não exibidos inline; disposition attachment por padrão. Cancelamentos limpam órfãos após janela segura.

Benchmarks ausentes. Riscos: egress, malware, inconsistência objeto/metadado, backup incompleto. Reconsiderar backend com custos de banda/recuperação medidos; testes com chaves de outro tenant são gate antes da ativação.
