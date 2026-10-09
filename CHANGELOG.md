# Changelog

Todas as mudanças notáveis deste projeto são documentadas neste arquivo.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/). A primeira versão publicada será a `1.0.0`, no primeiro deploy em produção. O código veio de um projeto anterior, cujo histórico não é mantido aqui.

## [Não lançado]

### Adicionado

- Base de código importada: cursos, estudantes, matrículas e pagamentos, com autenticação JWT sobre ASP.NET Core Identity, PostgreSQL 17 e Redis.
- `.env.example` com todas as variáveis que a API lê: segredos sem valor, o resto com um valor de exemplo de desenvolvimento.
- `scripts/com-env.sh` e `scripts/com-env.ps1`: carregam o `.env` e executam a API (ou outro comando, como `dotnet ef`) com essas variáveis.
- `.gitattributes` e `.editorconfig` fixando LF, para que o `dotnet format --verify-no-changes` dê o mesmo resultado no Windows e no Linux.
- README com seção "Como testar" e links para a organização `tech-curse`.
- CI no GitHub Actions em todo pull request e push na `main`: restore, build Release sem warnings, `dotnet format --verify-no-changes` e `dotnet test`, com cache de NuGet.
- `global.json` fixando o SDK 10.0.401.
- Estrutura de testes: integração com `WebApplicationFactory` e Testcontainers (PostgreSQL 17 e Redis 7 reais) e testes unitários, em xUnit v3 sobre o Microsoft.Testing.Platform. Primeiros testes: health checks (`TRV-020` a `TRV-022`) e elegibilidade de pagamento (`PAG-008`).
- Cobertura de código e relatório de rastreabilidade entre especificação e testes no resumo de cada execução do CI.

### Alterado

- Matrícula responde `201 Created`; antes respondia `202 Accepted` para uma matrícula já gravada (`MAT-001`).
- Acesso a dado de outro aluno (perfil, matrículas, pagamentos) e matrícula feita por `Instructor` respondem `403 Forbidden`; antes respondiam `409 Conflict`. Conflitos de estado, como processar um pagamento já pago, continuam `409` (`ALU-005`, `ALU-011`, `ALU-019`, `MAT-004`, `PAG-019`).
- O cache de consultas passa a ser compartilhado entre usuários, e toda escrita invalida a consulta de todos. Antes, cada usuário tinha a própria cópia, e só a de quem escrevia era apagada: alunos viam cursos editados ou removidos e pagamentos desatualizados por até 15 minutos (`TRV-019`, `CUR-018`, `PAG-023`).
- A repetição de uma escrita idempotente com a mesma `Idempotency-Key` devolve a resposta original; antes terminava em `500`, por causa de uma dupla serialização. A chave passa a separar usuário, método e rota (`TRV-015`, `TRV-016`).
- O nome do registro é livre: espaços, acentos e nomes repetidos são aceitos (ex.: `"João da Silva"`), com 1 a 100 caracteres. O `UserName` do Identity passa a ser o e-mail, e o nome fica só no perfil de estudante. Antes, o nome virava o `UserName`, que só aceita `A-Z a-z 0-9 - . _ @ +` e precisa ser único (`AUTH-009`).
- Versão reiniciada em `1.0.0` no `Directory.Build.props`.
- O Swagger passa a ler a versão do assembly, em vez de um `"2.0.0"` fixo no código.
- README corrigido: o lockout do Identity está configurado, mas o login ainda não o aplica (`lockoutOnFailure: false`).
- **A configuração vem só de `appsettings.json` e de variáveis de ambiente.** Para rodar localmente, copie o `.env.example` para `.env`, preencha as connection strings e a `Jwt__SigningKey`, e use `scripts/com-env`. Nenhuma credencial é versionada, nem de desenvolvimento.

### Removido

- Resposta `409` para "perfil de estudante sem usuário" no registro: a FK obrigatória com cascata torna esse estado impossível (`AUTH-006`, removido).
- `LICENSE`.
- `docs/diagram.png`, desatualizado (mostrava SQL Server e Seq); o diagrama em Mermaid do README é a referência.
- Histórico de versões do projeto anterior (1.x a 3.0.0) neste CHANGELOG.
- `appsettings.Development.json` e User Secrets (`UserSecretsId`): configuração por ambiente nomeado e uma segunda fonte da verdade ao lado do `.env`.

### Corrigido

- Requisições simultâneas de matrícula no mesmo curso (clique duplo) criavam matrículas duplicadas. Um índice único em (aluno, curso), na migration `MatriculaUnicaPorAlunoECurso`, barra a segunda, que responde `409` (`MAT-006`).

### Segurança

- Erro inesperado responde `500` com `"Ocorreu um erro inesperado. Informe o código de correlação ao suporte."`; antes o `detail` trazia a mensagem interna da exceção (PostgreSQL, Redis, JWT). O log da exceção passa a carregar o mesmo `CorrelationId` da resposta (`TRV-004`).
- O login responde igual, `401` com `"E-mail ou senha incorretos."`, para e-mail inexistente e para senha errada. Antes, a senha errada respondia `"Usuário não autenticado."`, o que revelava quais e-mails estão cadastrados; o Swagger também documentava um `400` que não existe (`AUTH-016`).
- Remover um aluno passa a bloquear de fato a conta dele. O handler só bloqueava se a navegação `Student.IdentityUser` estivesse carregada, e ela nunca estava: o aluno removido continuava fazendo login (`ALU-014`).
- O refresh recusa com `401` uma conta bloqueada e apaga o refresh token dela; antes, uma sessão já aberta era renovada indefinidamente mesmo com a conta bloqueada (`AUTH-039`, `ALU-016`).
- O refresh responde `401` para access token malformado, com assinatura de outra chave ou com outro algoritmo, e para usuário que não existe mais. Antes respondia `500` com a mensagem interna da biblioteca de JWT, `403` ou `404` (`AUTH-027`, `AUTH-028`).
- A chave de assinatura do JWT é convertida em UTF-8 na emissão e na validação; antes a emissão usava ASCII, e uma chave com acento gerava tokens que a própria API recusava (`AUTH-036`).
- O seed do Admin de desenvolvimento não grava mais o e-mail configurado no log de aviso; a mensagem cita só a chave `Seed:Admin:Email` (alerta `cs/exposure-of-sensitive-information` do CodeQL).
