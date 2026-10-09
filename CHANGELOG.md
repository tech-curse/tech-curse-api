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

- Versão reiniciada em `1.0.0` no `Directory.Build.props`.
- O Swagger passa a ler a versão do assembly, em vez de um `"2.0.0"` fixo no código.
- README corrigido: o lockout do Identity está configurado, mas o login ainda não o aplica (`lockoutOnFailure: false`).
- **A configuração vem só de `appsettings.json` e de variáveis de ambiente.** Para rodar localmente, copie o `.env.example` para `.env`, preencha as connection strings e a `Jwt__SigningKey`, e use `scripts/com-env`. Nenhuma credencial é versionada, nem de desenvolvimento.

### Removido

- `LICENSE`.
- `docs/diagram.png`, desatualizado (mostrava SQL Server e Seq); o diagrama em Mermaid do README é a referência.
- Histórico de versões do projeto anterior (1.x a 3.0.0) neste CHANGELOG.
- `appsettings.Development.json` e User Secrets (`UserSecretsId`): configuração por ambiente nomeado e uma segunda fonte da verdade ao lado do `.env`.

### Segurança

- Remover um aluno passa a bloquear de fato a conta dele. O handler só bloqueava se a navegação `Student.IdentityUser` estivesse carregada, e ela nunca estava: o aluno removido continuava fazendo login (`ALU-014`).
- O refresh recusa com `401` uma conta bloqueada e apaga o refresh token dela; antes, uma sessão já aberta era renovada indefinidamente mesmo com a conta bloqueada (`AUTH-039`, `ALU-016`).
- O refresh responde `401` para access token malformado, com assinatura de outra chave ou com outro algoritmo, e para usuário que não existe mais. Antes respondia `500` com a mensagem interna da biblioteca de JWT, `403` ou `404` (`AUTH-027`, `AUTH-028`).
- A chave de assinatura do JWT é convertida em UTF-8 na emissão e na validação; antes a emissão usava ASCII, e uma chave com acento gerava tokens que a própria API recusava (`AUTH-036`).
- O seed do Admin de desenvolvimento não grava mais o e-mail configurado no log de aviso; a mensagem cita só a chave `Seed:Admin:Email` (alerta `cs/exposure-of-sensitive-information` do CodeQL).
