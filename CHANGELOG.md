# Changelog

Todas as mudanças notáveis deste projeto são documentadas neste arquivo.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/). A primeira versão publicada será a `1.0.0`, no primeiro deploy em produção. O código veio de um projeto anterior, cujo histórico não é mantido aqui.

## [Não lançado]

### Adicionado

- Base de código importada: cursos, estudantes, matrículas e pagamentos, com autenticação JWT sobre ASP.NET Core Identity, PostgreSQL 17 e Redis.
- `.env.example` com todas as variáveis de configuração, sem valores.
- `.gitattributes` e `.editorconfig` fixando LF, para que o `dotnet format --verify-no-changes` dê o mesmo resultado no Windows e no Linux.
- README com seção "Como testar" e links para a organização `tech-curse`.
- CI no GitHub Actions em todo pull request e push na `main`: restore travado, build Release sem warnings, `dotnet format --verify-no-changes` e `dotnet test`.
- `global.json` fixando o SDK 10.0.401 e `packages.lock.json` em cada projeto, para restore reproduzível e cache de NuGet no CI.

### Alterado

- Versão reiniciada em `1.0.0` no `Directory.Build.props`.
- O Swagger passa a ler a versão do assembly, em vez de um `"2.0.0"` fixo no código.
- README corrigido: o lockout do Identity está configurado, mas o login ainda não o aplica (`lockoutOnFailure: false`).
- README e CLAUDE.md documentam que rodar localmente exige gravar as connection strings e a `Jwt:SigningKey` em User Secrets: nenhuma credencial é versionada, nem de desenvolvimento.

### Removido

- `LICENSE`.
- `docs/diagram.png`, desatualizado (mostrava SQL Server e Seq); o diagrama em Mermaid do README é a referência.
- Histórico de versões do projeto anterior (1.x a 3.0.0) neste CHANGELOG.
