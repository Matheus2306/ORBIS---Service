# Backup e recuperação

Plano candidato: base backup diário + WAL contínuo para PITR, retenção inicial 35 dias (aprovação contratual pendente), cópia criptografada fora da instância/zona e acesso segregado. Medir custo/tempo; backup lógico adicional pode apoiar exportação, mas não substitui PITR de RPO 5 min. Runtime não recebe credencial de backup.

Ensaio em ambiente isolado: selecionar ponto-alvo; registrar início; restaurar base+WAL; confirmar timeline e timestamp; testar checksums/contagens/FKs, RLS com role runtime, login e jornada; calcular RPO e RTO; anexar logs sanitizados e destruir cluster de ensaio somente após retenção da evidência. Promover endpoint do banco só no plano de incidente aprovado.

Periodicidade planejada: restore mensal e após mudanças de versão/storage; exercício de desastre trimestral. Não existe backup de produção nem ensaio executado ainda. Dump+restore de Small é teste funcional inicial, não prova do RTO de Large ou PITR.
