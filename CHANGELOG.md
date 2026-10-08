# 📋 Changelog

Todas as alterações notáveis deste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

---

## [Não lançado]

### ✨ Adicionado

- `.env.example` com todas as variáveis de configuração, sem valores.
- `.gitattributes` e `.editorconfig` fixando LF, para que o `dotnet format --verify-no-changes` dê o mesmo resultado no Windows e no Linux.
- README com seção "Como testar" e links para a organização `tech-curse`.

### 🔄 Alterado

- README corrigido: o lockout do Identity está configurado, mas o login ainda não o aplica (`lockoutOnFailure: false`).
- README e CLAUDE.md documentam que rodar localmente exige gravar as connection strings e a `Jwt:SigningKey` em User Secrets: nenhuma credencial é versionada, nem de desenvolvimento.

### 🗑️ Removido

- `LICENSE`: o repositório deixa de ser distribuído sob Apache 2.0.
- `docs/diagram.png`: desatualizado (mostrava SQL Server e Seq); o diagrama em Mermaid do README é a referência.

---

## [3.0.0] - 2026-10-08

### 🌟 Resumo Executivo

A **v3.0.0** troca o banco de SQL Server para PostgreSQL 17, fecha uma falha de segurança no registro público e passa a criar o perfil de aluno junto com o usuário. Também marca um recomeço da infraestrutura de qualidade e entrega: a base de testes, o build de imagem, o compose e o pipeline saem do repositório para serem reconstruídos do zero.

É uma **MAJOR** porque a connection string muda de formato, o registro público deixa de aceitar `role` e a aplicação deixa de ser distribuída como imagem de contêiner.

O build da solution roda com **0 erros e 0 warnings**, e o `dotnet format --verify-no-changes` passa limpo. Não há testes automatizados nesta versão.

### ⚠️ Breaking Changes

| Quebra | O que fazer |
| :--- | :--- |
| **Banco relacional passou de SQL Server 2022 para PostgreSQL 17** | A connection string `ConnectionStrings:APITechCurse` muda de formato: `Host=...;Port=5432;Database=APITechCurse;Username=...;Password=...;`. Não há migração de dados: as migrations foram regeradas do zero, e um banco SQL Server existente não é reaproveitável |
| Health check `Database_SQLServer` renomeado para `Database_Postgres` | Ajuste dashboards ou alertas que filtram pelo nome no detalhe de `/health/ready` |
| **Imagem, compose e pipeline removidos**: saem `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `docker-compose.ci.yml`, `.env.example` e o workflow `ci-cd.yml`. Nenhuma imagem nova é publicada no GHCR | Rode a API com `dotnet run --project src/Api` apontando para PostgreSQL 17 e Redis próprios. As imagens já publicadas (ex.: `ghcr.io/liuizn/tech-curse:2.0.0`) continuam disponíveis. Volumes antigos do compose podem ser descartados com `docker volume rm` |
| **Seq removido do projeto**: saem o pacote `Serilog.Sinks.Seq` e a chave `ConnectionStrings:SeqUrl` | Os logs estruturados continuam no console em JSON |
| `POST /tech-curse/Auth/register` não aceita mais `role`: o usuário é sempre criado como `Student` | Clientes que criavam Admin ou Instructor pelo registro passam a usar `POST /tech-curse/Auth/users` autenticados como Admin. O campo `role` enviado no registro é ignorado |

### 🔐 Segurança

- O registro público aceitava a role no corpo e permitia que qualquer pessoa se cadastrasse como `Admin`. Agora `POST /tech-curse/Auth/register` cria sempre `Student`.

### ✨ Adicionado

- `POST /tech-curse/Auth/users` (Admin): cria usuário com role `Admin`, `Instructor` ou `Student`.
- Admin semeado em `Development` a partir de `Seed:Admin:Email`/`Seed:Admin:Password` (user-secrets ou `SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` no compose).
- O cadastro de aluno (`POST /tech-curse/Auth/register`, e `POST /tech-curse/Auth/users` com role `Student`) cria o perfil de estudante automaticamente; se a gravação falhar, o usuário é removido. E-mail com perfil de estudante sem usuário responde 409; e-mail de usuário já existente continua 422 `DuplicateEmail`.
- `PaymentOutputDto` ganha `courseId` e `courseTitulo`; `CourseStudentOutputDto` (matrículas do estudante) ganha `enrollmentId`.

### 🔄 Alterado

- `Microsoft.EntityFrameworkCore.SqlServer` → `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; `AspNetCore.HealthChecks.SqlServer` → `AspNetCore.HealthChecks.NpgSql`.
- O filtro do índice único de `Payment.EnrollmentId` passou de `[IsActive] = 1` para `"IsActive" = true`; o comportamento (um pagamento ativo por matrícula, reativação após desativar) foi verificado contra Postgres real.
- Documentação (README, collection do Postman e CLAUDE.md) atualizada; o CLAUDE.md registra as armadilhas novas: `DateTime` precisa ser UTC, `HasFilter` é SQL cru no dialeto do provider, e troca de provider exige regerar migrations.
- Montagem do `PaymentOutputDto` centralizada em `PaymentMapping`; prefixos de cache de pagamento centralizados em `ChavesDeCachePagamento` e versionados (`payments:v2:`), para não servir respostas no formato antigo.
- Usuários `Student` criados antes desta versão continuam sem perfil; o Admin cria pelo `POST /tech-curse/Student`.
- `PaymentRepository.UpdateAsync` marca só o pagamento como modificado, não suas navegações — processar/estornar não reescreve mais linhas de matrícula, curso ou aluno.

### 🗑️ Removido

- **Base de testes**: os projetos `TechCurse.Domain.UnitTests`, `TechCurse.Application.UnitTests`, `TechCurse.Api.IntegrationTests` e `TechCurse.ArchitectureTests`, o `tests/Directory.Build.props` e as versões de pacote usadas só por eles (xUnit, FluentAssertions, Moq, NetArchTest, coverlet, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.NET.Test.Sdk` e `Microsoft.EntityFrameworkCore.InMemory`). A estrutura de testes será reconstruída do zero.
- **Ganchos de teste em `src/`**: a chave `UseInMemoryDatabase` no `EFCoreSetup`, a guarda `IsRelational()` em volta do `Migrate()` e o `public partial class Program { }`.
- **Build de imagem e pipeline**: ver Breaking Changes. O build da solution e o `dotnet format` continuam limpos, mas nada mais os verifica automaticamente.

### 📌 Mantido de propósito

- `InvariantGlobalization=false` continua, embora o motivo original (`Microsoft.Data.SqlClient`) tenha saído. Validar se o Npgsql dispensa o ICU fica para quando a nova imagem for construída.

---

## [2.0.0] - 2026-08-25

### 🌟 Resumo Executivo

A **v2.0.0** consolida uma reestruturação completa do repositório e um endurecimento de segurança e de entrega. O código foi realinhado às convenções de mercado para Clean Architecture em .NET, três defeitos capazes de derrubar ou corromper produção foram corrigidos, e o pipeline foi reescrito para que uma imagem defeituosa não chegue mais ao registry.

É uma **MAJOR** porque há quebras que exigem ação de quem consome a API: a rota `/health` deixou de existir, respostas `429` passaram a ser possíveis em qualquer endpoint, todos os refresh tokens em circulação foram invalidados e a imagem mudou de registry.

A suíte foi de 206 para **229 testes**, e o build roda com **0 erros e 0 warnings**.

---

### ⚠️ Breaking Changes & Guia de Migração

| Quebra | O que fazer |
| :--- | :--- |
| `GET /health` responde **404** | Use `/health/live` para liveness (não consulta dependências) ou `/health/ready` para readiness (agrega SQL Server e Redis) |
| Detalhe de `/health/ready` restrito | O corpo detalhado por verificação agora exige JWT de role `Admin`. Chamadores anônimos recebem apenas `{"status":"..."}` — suficiente para sonda de orquestrador |
| **`429 Too Many Requests`** é resposta nova | Trate o status e respeite o header `Retry-After`. Limites padrão: 200 req/min global, 10 req/min nos endpoints de autenticação |
| **Refresh tokens invalidados** | Os valores gravados em texto puro não casam com o novo hash SHA-256. Todos os usuários precisam fazer login novamente após o deploy |
| **Registry mudou** | `docker pull ghcr.io/liuizn/tech-curse:2.0.0` — a imagem não é mais publicada no Docker Hub |
| **Produção não sobe sem gateway real** | `AddInfrastructure` lança `InvalidOperationException` quando `ASPNETCORE_ENVIRONMENT=Production`, porque só existe `SimulatedPaymentGatewayAdapter`. Implemente um adaptador real antes de subir em produção |
| **Falha de migration derruba o startup** | O erro deixou de ser engolido. Garanta que o banco esteja acessível e migrável antes de subir a aplicação |
| Assemblies e namespaces renomeados | `TechCurse.src.<Camada>` → `TechCurse.<Camada>`; assemblies `tech-curse.*` → `TechCurse.*` |

---

### 🐛 Corrigido

- **Duas `NullReferenceException` vivas em produção.** `PaymentRepository.GetByIdAsync` e `GetByEnrollmentIdAsync` usavam `AsNoTracking()` sem `Include`, enquanto os handlers acessavam `payment.Student.IdentityUserId` e `.Enrollment.Student`. Sem lazy loading — `UseLazyLoadingProxies()` nunca é chamado e as navegações não são `virtual` — a navegação vinha `null`. Os testes unitários não pegavam porque mockam o repositório devolvendo a navegação preenchida à mão.
- **Gateway simulado registrado em Production.** O ramo `if (environment.IsProduction())` registrava a mesma implementação do ramo `else`, fazendo a API confirmar cobranças que nunca aconteceram. Agora aborta o startup com mensagem explícita.
- **Migrations falhando em silêncio.** O `catch` no `Program.cs` logava e seguia, deixando a API servindo requisições contra um banco sem schema.
- **`InvariantGlobalization` incompatível com o SQL Server.** O `Microsoft.Data.SqlClient` lança `NotSupportedException: Globalization Invariant Mode is not supported` ao abrir conexão, derrubando o `Migrate()` e o health check `Database_SQLServer`. A propriedade voltou a `false` e a imagem runtime passou à variante chiseled `-extra`, que inclui o ICU.
- **Duas regras de arquitetura passavam vazias.** `Commands_Should_Have_NameEndingWith_Command` e a equivalente de Queries ainda casavam o namespace `TechCurse\.src\.Application` numa regex; como não casavam nada, passavam sem verificar convenção alguma.
- **Divergência de versão do FluentValidation**: produção resolvia `11.10.0` (transitivo) enquanto os testes de validator rodavam contra `12.1.1`.
- **Versão flutuante** `Microsoft.AspNetCore.Mvc.Testing 10.0.0-*` fixada em `10.0.11`.
- **Build limpo**: 22 warnings (`CS8618`, `CS8602`, `CS8604`, `CS8601`, `NU1510`) e 11 `xUnit1012` eliminados.
- `TechCurse.Application.UnitTests` referenciava `TechCurse.Api` sem usar e dependia de `TechCurse.Infrastructure` só por transitividade.
- `Dockerfile` passou a copiar `Directory.Build.props` e `Directory.Packages.props`, sem os quais o restore com CPM falha na imagem.
- Badge e seção de licença do README diziam MIT contra um `LICENSE` Apache 2.0.

### 🔐 Segurança

- **Refresh token protegido.** Passou a ser persistido como **SHA-256** em `AspNetUserTokens`, com comparação em tempo constante via `CryptographicOperations.FixedTimeEquals` e expiração própria (`Jwt:RefreshTokenDays`, padrão 7 dias). Antes era gravado em texto puro e comparado com `!=`.
- **Rate limiting HTTP.** Limiter global de 200 req/min particionado por usuário autenticado (ou por IP quando anônimo) e política dedicada de 10 req/min por IP nos endpoints de autenticação. Rejeição devolve `ProblemDetails` 429 com `Retry-After`.
- **Data Protection persistido no banco.** O chaveiro ia para `/home/app/.aspnet` dentro do contêiner e sumia a cada recriação, invalidando cookies e tokens de reset. Agora vive na tabela `DataProtectionKeys`, com `SetApplicationName` fixo.
- **`/health/ready` deixou de vazar detalhe de infraestrutura.** Mensagens de exceção com endereço e nome de servidor agora exigem role `Admin`.
- **Credenciais de exemplo removidas do `.env.example`**, substituídas por placeholders.

### ✨ Adicionado

- **`TechCurse.Domain.UnitTests`** — quarto projeto de teste, com os testes de entidades e Specifications que viviam no projeto de unitários de aplicação. Referencia exclusivamente `TechCurse.Domain`.
- **23 testes novos**, cobrindo o que nunca era exercitado: navegações de pagamento com aluno ativo e soft-deleted, liveness e readiness com corte por role, rate limiting, persistência do chaveiro de Data Protection, montagem do container em Production, recusa de subir com banco inalcançável, e **drift entre modelo e migrations** (`HasPendingModelChanges`) — as migrations nunca haviam sido exercitadas, porque o provider InMemory monta o schema a partir do modelo.
- **Separação de liveness e readiness** com tags (`HealthCheckTags.Ready`).
- **`docker-compose.ci.yml`** — override de pipeline que valida a imagem já construída, sem reconstruir, e sem expor portas de infraestrutura no runner.
- **Versionamento SemVer 2.0.0** declarado em `Directory.Build.props` e propagado ao Swagger e às tags da imagem.
- `SwaggerDocumentTests` e `CustomWebApplicationFactory.EnvironmentName` virtual.

### ♻️ Alterado

- **Estrutura de pastas padronizada**: `src/` e `tests/` na raiz, no estilo do template `dotnet new ca-sln`. A pasta `tech-curse/` intermediária foi eliminada e `src/API` passou a `src/Api`.
- **Projetos e assemblies renomeados** de `tech-curse.*` para `TechCurse.*`, restabelecendo `.csproj` = `AssemblyName` = `RootNamespace`. Solution passou a `TechCurse.slnx`, com solution folders espelhando o disco.
- **Namespaces** `TechCurse.src.<Camada>` → `TechCurse.<Camada>`; migrations de `TechCurse.Migrations` → `TechCurse.Infrastructure.Migrations`.
- **Central Package Management**: versões centralizadas em `Directory.Packages.props`, propriedades comuns em `Directory.Build.props` e `tests/Directory.Build.props`.
- **Pacotes atualizados**: stack Microsoft/EF Core para `10.0.11`, Serilog.AspNetCore `8.0.3`→`10.0.0`, Serilog.Sinks.Seq `8.0.0`→`9.1.0`, StackExchange.Redis `3.0.17`→`3.1.31`, Swashbuckle `6.6.2`→`10.2.3`, xUnit `2.5.3`→`2.9.3`, Test SDK `17.8.0`→`18.9.0`, coverlet `6.0.0`→`10.0.1`. MediatR permanece em `12.4.1` de propósito — a `13.0` passou a exigir licença comercial.
- **`SwaggerDocumentationSetup` migrado para Microsoft.OpenApi 2.x**: namespace `Microsoft.OpenApi.Models` achatado, `OpenApiReference` substituído por `OpenApiSecuritySchemeReference`.
- **`BadRequestExecption` renomeada para `BadRequestException`**.
- **Soft delete**: decisão tomada de preservar o histórico financeiro — consultas de `Payment` que atravessam a navegação usam `IgnoreQueryFilters()`.
- **Todos os comentários removidos do código-fonte**, em `.cs` e nos arquivos de infraestrutura. A justificativa de cada decisão vive no `CLAUDE.md` e nas mensagens de commit.
- `.editorconfig` adicionado; `dotnet format --verify-no-changes` passa limpo.
- `PaymentPersistenceTests` convertido de ISO-8859-1 para UTF-8.

### 🚀 CI/CD

- **A ordem foi invertida para build → smoke test → push.** O login no registry acontece depois do teste, então imagem que não sobe nunca chega ao GHCR. Antes, toda execução publicava em `:latest` mesmo falhando.
- **Migração do Docker Hub para o GitHub Container Registry**, eliminando dois segredos de registry — o `GITHUB_TOKEN` embutido basta.
- **Tags de imagem por versão e por commit** (`2.0.0`, `2.0`, `2`, `sha-<commit>`) além de `latest`, viabilizando rollback e rastreabilidade.
- **Diagnóstico em caso de falha**: logs de `api`, `db` e `redis` são despejados quando o smoke test quebra. Foi o que permitiu diagnosticar o bug de globalização depois de cinco execuções vermelhas sem pista.
- **Espera por tentativas** no lugar de `sleep 20` fixo; a API costuma responder em ~10s.
- Cache de NuGet e de camadas Docker, `permissions` de menor privilégio, `concurrency` com cancelamento, publicação de resultados de teste e cobertura, e auditoria de pacotes vulneráveis.
- **Imagem chiseled com digest pinado** e labels OCI ligando o pacote ao repositório.

> Distribuição dos 229 testes: 23 arquitetura, 6 domínio, 133 aplicação, 67 integração.

---

## [1.2.0] - 2026-08-16

### 🌟 Resumo Executivo
A versão **v1.2.0** representa um marco arquitetural na evolução da **Tech Curse API**. O projeto foi completamente reestruturado da arquitetura em camadas tradicional baseada em serviços monolíticos para uma arquitetura moderna baseada em **Clean Architecture** e **CQRS (Command Query Responsibility Segregation)** com **Vertical Slices**, utilizando **MediatR** e **FluentValidation**.

Além disso, esta release consolida uma suíte robusta de **204 testes automatizados** com 100% de taxa de aprovação, abrangendo testes de integridade arquitetural (NetArchTest), testes unitários em isolamento e testes de integração de ponta a ponta.

---

### 🚀 Principais Destaques & Mudanças

#### 1. Migração Total para CQRS & MediatR
- **Desacoplamento Completo de Serviços:** As antigas classes monolíticas (`CourseService`, `StudentService`, `EnrollmentService`, `PaymentService`) foram 100% removidas.
- **Vertical Slices Isoladas:** Cada operação de leitura e escrita do sistema agora é tratada por um **Command Handler** ou **Query Handler** dedicado e autocontido.
- **Injeção Cirúrgica de Dependências:** Handlers de consulta não instanciam dependências pesadas de escrita ou de gateway, resultando em menor consumo de memória e ganhos expressivos de performance em requisições concorrentes.

#### 2. Validação Determinística com FluentValidation & Pipeline Behaviors
- Implementação do `ValidationBehavior<TRequest, TResponse>` no pipeline do MediatR.
- Entradas inválidas ou em formatos incorretos são interceptadas antes de interagir com o banco de dados.
- Padronização de respostas de erro no formato RFC 7807 (`ProblemDetails`) com status **`422 Unprocessable Entity`**, listando o detalhamento de cada campo violado.

#### 3. Eliminação de God Objects & Quebra do Módulo Payment
- A entidade e operações de `Payment` foram desmembradas em 7 Vertical Slices independentes:
  - `CreatePaymentCommand` / `CreatePaymentCommandHandler`
  - `ProcessPaymentCommand` / `ProcessPaymentCommandHandler`
  - `RefundPaymentCommand` / `RefundPaymentCommandHandler`
  - `GetPaymentsQuery` / `GetPaymentsQueryHandler`
  - `GetPaymentByIdQuery` / `GetPaymentByIdQueryHandler`
  - `GetPaymentsByStudentIdQuery` / `GetPaymentsByStudentIdQueryHandler`
  - `GetPaymentsByEnrollmentIdQuery` / `GetPaymentsByEnrollmentIdQueryHandler`
- Introdução da especificação de domínio `PaymentProcessableSpecification` para blindar regras de transição de estado de pagamentos.

#### 4. Correção e Padronização de Rotas RESTful
- Correção de rotas legadas fora do padrão:
  - `POST /process` ➔ **`POST /tech-curse/payment/process`**
  - `POST /refund` ➔ **`POST /tech-curse/payment/refund`**
- Todos os controladores agora utilizam o prefixo padronizado `/tech-curse/[controller]`.

#### 5. Cache Distribuído com Redis, Idempotência & Observabilidade
- Implementação de middleware de idempotência (`IdempotencyFilterMiddleware`) com armazenamento em Redis para evitar processamento duplicado de pagamentos.
- Rastreamento fim a fim de requisições com `CorrelationIdMiddleware`.
- Integração com Serilog e sink para Seq Dashboard (`http://localhost:9000`).

#### 6. Suíte de 204 Testes Automatizados (100% Passing)
- **Testes de Arquitetura (`tech-curse.Test.Architecture` - 23 testes):** Governança automatizada via NetArchTest para garantir a integridade das camadas da Clean Architecture (nenhuma dependência inversa para o Domain).
- **Testes Unitários (`tech-curse.Test.Unit` - 143 testes):** Cobertura unitária exaustiva de todos os Handlers, Validators do FluentValidation e Domain Specifications com `Moq` e `FluentAssertions`.
- **Testes de Integração (`tech-curse.Test.Integration` - 38 testes):** Testes de ponta a ponta com `WebApplicationFactory` cobrindo autenticação JWT, autorização RBAC, middlewares e persistência em memória.

---

### ⚠️ Breaking Changes & Guia de Migração

| Endpoint / Componente Antigo | Novo Endpoint / Comportamento v1.2.0 | Status Code Atualizado | Impacto / Ação Necessária |
| :--- | :--- | :---: | :--- |
| `POST /process` | `POST /tech-curse/payment/process` | `200 OK` | Atualizar URLs de chamadas de processamento de pagamento. Retorna `ProcessPaymentOutputDto`. |
| `POST /refund` | `POST /tech-curse/payment/refund` | `200 OK` | Atualizar URLs de chamadas de estorno de pagamento. Retorna `RefundPaymentOutputDto`. |
| Validação de DTOs | Interceptado pelo MediatR Pipeline | `422 Unprocessable Entity` | Clientes devem tratar `422` com `ValidationExceptionResponse` (ProblemDetails) em vez de `400 Bad Request` genérico. |
| Aluno ou Curso inexistente na Matrícula | `NotFoundException` | `404 Not Found` | Tratamento semântico corrigido (anteriormente retornava `403 Forbidden` indevido). |
| Aluno já matriculado no Curso | `ConflictException` | `409 Conflict` | Tratamento semântico corrigido (anteriormente retornava `403 Forbidden` indevido). |
| Handlers de Leitura / Escrita | Substituição de Services por MediatR Requests | N/A Interno | Classes de serviço `*Service` foram removidas em favor de `IRequest` / `IRequestHandler`. |

---

### 👥 Contribuidores
- [@Liuizn](https://github.com/Liuizn)
