# Changelog

Todas as mudanças notáveis deste projeto são documentadas neste arquivo.

O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/). A primeira versão publicada será a `1.0.0`, no primeiro deploy em produção. O código veio de um projeto anterior, cujo histórico não é mantido aqui.

## [Não lançado]

### Adicionado

- Comandos administrativos na imagem da API: `migrar` aplica as migrations e cria as roles, e `criar-admin` cria o primeiro Admin a partir de `CriarAdmin__Email` e `CriarAdmin__Password` (`TRV-024`, `TRV-035`). O compose de desenvolvimento roda o `migrar` antes da API.
- `POST /Auth/logout`: encerra a sessão do refresh token informado e responde `204` sempre (`AUTH-032`).
- Base de código importada: cursos, estudantes, matrículas e pagamentos, com autenticação JWT sobre ASP.NET Core Identity, PostgreSQL 17 e Redis.
- `.env.example` com todas as variáveis que a API lê: segredos sem valor, o resto com um valor de exemplo de desenvolvimento.
- `scripts/com-env.sh` e `scripts/com-env.ps1`: carregam o `.env` e executam a API (ou outro comando, como `dotnet ef`) com essas variáveis.
- `.gitattributes` e `.editorconfig` fixando LF, para que o `dotnet format --verify-no-changes` dê o mesmo resultado no Windows e no Linux.
- README com seção "Como testar" e links para a organização `tech-curse`.
- CI no GitHub Actions em todo pull request e push na `main`: restore, build Release sem warnings, `dotnet format --verify-no-changes` e `dotnet test`, com cache de NuGet.
- `global.json` fixando o SDK 10.0.401.
- Estrutura de testes: integração com `WebApplicationFactory` e Testcontainers (PostgreSQL 17 e Redis 7 reais) e testes unitários, em xUnit v3 sobre o Microsoft.Testing.Platform. Primeiros testes: health checks (`TRV-020` a `TRV-022`) e elegibilidade de pagamento (`PAG-008`).
- Cobertura de código e relatório de rastreabilidade entre especificação e testes no resumo de cada execução do CI.

- Imagem de contêiner da API (`Dockerfile`): build multi-stage, runtime `chiseled-extra` sem shell e com ICU, usuário não root (`app`) e healthcheck pela sonda `TechCurse.Sonda`, que consulta `/health/ready`.
- `compose.yaml` de desenvolvimento com PostgreSQL 17, Redis 7 e a API, portas só em `127.0.0.1`.
- Job `imagem` no CI: constrói a imagem, sobe o compose e verifica usuário não root, health checks, login e encerramento limpo (`scripts/verificar-conteiner.sh`).
- Dependabot acompanha as imagens do `Dockerfile` e do `compose.yaml`.
- Publicação da imagem no GHCR (`ghcr.io/tech-curse/tech-curse-api`) depois de os jobs `ci` e `imagem` passarem: `sha-<commit>` a cada push na `main` e `X.Y.Z` a cada tag de versão, nunca `latest`, com SBOM e atestação de proveniência.

### Alterado

- **A API não altera mais o banco ao subir.** As migrations e as roles passam a ser aplicadas pelo comando `migrar`, inclusive o Admin de `Development` (`AUTH-038`). Fora do compose, rode `./scripts/com-env.sh dotnet run --project src/Api -- migrar` antes da API.
- O módulo de pagamentos é ligado por `Payments:Enabled` (padrão `false`). Desligado, as rotas de `/tech-curse/Payment` respondem `404` e somem do Swagger. A API passa a subir em `Production` com os pagamentos desligados; com eles ligados, continua se recusando, porque o único gateway é o simulado (`TRV-025`, `TRV-033`). O `.env.example` de desenvolvimento liga a chave.
- O Swagger é ligado por `Swagger:Enabled` (padrão `false`), não mais pelo nome do ambiente (`Development` ou `Homolog`). O `.env.example` de desenvolvimento liga a chave (`TRV-026`).
- As mensagens de erro do Identity (senha, e-mail duplicado ou inválido) passam a vir em português; antes vinham em inglês e apareciam assim no formulário do web (`AUTH-004`, `AUTH-005`, `AUTH-007`).
- No `SIGTERM`, a API espera até 25 segundos pelas requisições em andamento e termina com código `0`; o compose espera 30 segundos antes de matá-la (`TRV-036`).

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

- `UseHttpsRedirection()`: o TLS termina no Nginx, e a API atende só HTTP (`TRV-037`).
- Resposta `409` para "perfil de estudante sem usuário" no registro: a FK obrigatória com cascata torna esse estado impossível (`AUTH-006`, removido).
- `LICENSE`.
- `docs/diagram.png`, desatualizado (mostrava SQL Server e Seq); o diagrama em Mermaid do README é a referência.
- Histórico de versões do projeto anterior (1.x a 3.0.0) neste CHANGELOG.
- `POST /Student`: criava perfil de aluno para um usuário já existente, situação que não ocorre desde que o registro cria o perfil. Também respondia `409` para usuário inexistente, criava perfil de aluno para um `Instructor` e podia dar um segundo perfil a quem tinha um removido. Agora responde `405` (`ALU-020`).
- `appsettings.Development.json` e User Secrets (`UserSecretsId`): configuração por ambiente nomeado e uma segunda fonte da verdade ao lado do `.env`.

### Corrigido

- Duas criações simultâneas de pagamento para a mesma matrícula: a segunda respondia `500` com a mensagem interna do PostgreSQL; agora responde `409` `"Já existe um pagamento ativo para esta matrícula."` (`PAG-004`).
- Processar com `Pix` ou `Boleto` responde `422` com `errors.Type` = `"Método de pagamento não suportado."`; antes respondia `500` (`PAG-011`).
- O `Admin` vê pela rota por aluno os pagamentos de um aluno removido; antes recebia `404`, embora o histórico financeiro deva sobreviver à remoção (`PAG-020`).
- Os pagamentos de uma matrícula sem nenhum pagamento vêm como lista vazia (`200`); antes a resposta era `404`. Matrícula inexistente responde `404` `"Matrícula não encontrada."` (`PAG-022`).
- `PageNumber` ou `PageSize` menor que `1` responde `422` com o parâmetro em `errors`. Antes, página `0` respondia `500` e tamanho `0` devolvia um `totalPages` sem sentido (`TRV-031`).
- A listagem de alunos tem ordem estável: por id, ou por `nome` / `datacadastro` com `SortBy` e `SortDirection`. Antes não tinha ordenação, e um aluno podia se repetir ou sumir entre páginas (`ALU-008`).
- A criação de curso limita a categoria a 50 caracteres, como a edição; antes um curso podia ser criado com uma categoria que depois impedia a edição (`CUR-009`).
- Requisições simultâneas de matrícula no mesmo curso (clique duplo) criavam matrículas duplicadas. Um índice único em (aluno, curso), na migration `MatriculaUnicaPorAlunoECurso`, barra a segunda, que responde `409` (`MAT-006`).

### Segurança

- Atrás do proxy reverso, o IP do cliente vem de `X-Forwarded-For`, aceito só dos proxies configurados em `ForwardedHeaders:KnownProxies` e `ForwardedHeaders:KnownNetworks`. Sem isso, o limite de requisições contaria todos os usuários como o IP do Nginx (`TRV-013`).
- O refresh token sai do corpo das respostas e passa a viajar só num cookie `HttpOnly`, `Secure`, `SameSite=Strict`, restrito a `/tech-curse/Auth`, fora do alcance do JavaScript. `POST /Auth/refresh` e `POST /Auth/logout` leem o cookie, sem corpo; o refresh não recebe mais o access token. O access token passa de 2 horas para 15 minutos (`Jwt:AccessTokenMinutes`) (`AUTH-034`).
- Sessões de autenticação em tabelas próprias (`SessoesDeAutenticacao` e `RefreshTokens`), no lugar do `AspNetUserTokens`. Um usuário pode ficar logado em vários aparelhos, e encerrar um não afeta os outros (`AUTH-031`); antes, um novo login derrubava o anterior. A reapresentação de um refresh token já usado revoga a sessão inteira, inclusive o token mais novo (`AUTH-033`).
- Política de senha: de 12 a 128 caracteres, mantendo minúscula, maiúscula, número e símbolo; antes o mínimo era 8. Vale para senhas novas: contas existentes continuam entrando (`AUTH-004`).
- Cinco senhas erradas seguidas bloqueiam a conta por 15 minutos, com a mesma resposta genérica do login errado. O bloqueio estava configurado no Identity, mas o login não o aplicava (`AUTH-018`).
- Erro inesperado responde `500` com `"Ocorreu um erro inesperado. Informe o código de correlação ao suporte."`; antes o `detail` trazia a mensagem interna da exceção (PostgreSQL, Redis, JWT). O log da exceção passa a carregar o mesmo `CorrelationId` da resposta (`TRV-004`).
- O login responde igual, `401` com `"E-mail ou senha incorretos."`, para e-mail inexistente e para senha errada. Antes, a senha errada respondia `"Usuário não autenticado."`, o que revelava quais e-mails estão cadastrados; o Swagger também documentava um `400` que não existe (`AUTH-016`).
- Remover um aluno passa a bloquear de fato a conta dele. O handler só bloqueava se a navegação `Student.IdentityUser` estivesse carregada, e ela nunca estava: o aluno removido continuava fazendo login (`ALU-014`).
- O refresh recusa com `401` uma conta bloqueada e apaga o refresh token dela; antes, uma sessão já aberta era renovada indefinidamente mesmo com a conta bloqueada (`AUTH-039`, `ALU-016`).
- O refresh responde `401` para access token malformado, com assinatura de outra chave ou com outro algoritmo, e para usuário que não existe mais. Antes respondia `500` com a mensagem interna da biblioteca de JWT, `403` ou `404` (`AUTH-027`, `AUTH-028`).
- A chave de assinatura do JWT é convertida em UTF-8 na emissão e na validação; antes a emissão usava ASCII, e uma chave com acento gerava tokens que a própria API recusava (`AUTH-036`).
- O seed do Admin de desenvolvimento não grava mais o e-mail configurado no log de aviso; a mensagem cita só a chave `Seed:Admin:Email` (alerta `cs/exposure-of-sensitive-information` do CodeQL).
