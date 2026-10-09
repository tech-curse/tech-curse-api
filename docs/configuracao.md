# Configuração da Tech Curse API

Como a API recebe configuração, onde cada valor mora e de onde ele vem em cada contexto. É a aplicação do fator III do [Twelve-Factor](twelve-factor.md) e o detalhe dos cenários `TRV-034` e `TRV-039` da [especificação](especificacoes/transversais.md).

## O princípio

**A API conhece duas fontes: `appsettings.json` e variáveis de ambiente.** O `appsettings.json` traz os padrões seguros, iguais em todo ambiente; as variáveis de ambiente prevalecem e trazem tudo o que muda entre ambientes, segredos incluídos.

A API **não lê arquivo `.env`**. O `.env` é só um jeito prático de guardar variáveis de ambiente: quem inicia o processo (a IDE, o script `scripts/com-env`, o Docker Compose) o transforma em variáveis antes de a API subir. Assim o código é o mesmo em qualquer contexto, e só muda quem fornece as variáveis.

Não usamos `appsettings.<Ambiente>.json` (configuração agrupada por ambiente, que o Twelve-Factor desaconselha) nem User Secrets (específico de `Development`, em JSON sem criptografia, e uma segunda fonte da verdade ao lado do `.env` que o compose já usa).

Em variáveis de ambiente, o separador de seção é `__`: `Jwt__SigningKey` corresponde a `Jwt:SigningKey`.

## Onde cada chave mora

| Tipo | Exemplos | Onde fica |
| --- | --- | --- |
| Padrão de política: igual em todo ambiente | `Logging:*`, `RateLimiting:*`, `Jwt:RefreshTokenDays` e, quando existirem, `Payments:Enabled` e `Swagger:Enabled` com valor `false` | `appsettings.json` (versionado) |
| Varia por ambiente, não secreto | `ASPNETCORE_ENVIRONMENT`, `Jwt__Issuer`, `Jwt__Audience`, `Cors__AllowedOrigins` e, quando existirem, `Payments__Enabled`, `Swagger__Enabled`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_RESOURCE_ATTRIBUTES` | Variável de ambiente; no `.env.example` **com** um valor de exemplo de desenvolvimento |
| Segredo | `ConnectionStrings__APITechCurse`, `ConnectionStrings__RedisCache`, `Jwt__SigningKey`, `Seed__Admin__Email`, `Seed__Admin__Password` e as credenciais do comando de criação do primeiro Admin (`TRV-035`) | Variável de ambiente; no `.env.example` **sem** valor |

Uma chave nova entra no `appsettings.json` se tiver um padrão seguro, e sempre no `.env.example`. **Toda chave que a API lê está no `.env.example`**, para quem configura um ambiente novo ter a lista completa num lugar só.

## De onde as variáveis vêm em cada contexto

| Contexto | Fonte das variáveis | Segredos reais? |
| --- | --- | --- |
| **Desenvolvimento, API no host** (`dotnet run`, debug na IDE, `dotnet ef`) | `.env` local, carregado pela run configuration da IDE (Rider e VS Code aceitam arquivo de ambiente) ou pelo script `scripts/com-env` | Não: valores descartáveis da sua máquina |
| **Desenvolvimento, tudo em contêiner** | O mesmo `.env`, lido pelo compose de desenvolvimento (Fase 4) | Não |
| **Testes de integração** | A factory dos testes injeta a configuração em memória, com as connection strings dos contêineres do Testcontainers. Nenhum `.env` | Não |
| **CI** | Variáveis declaradas no workflow; valores sensíveis, como a chave do JWT dos testes end-to-end, são gerados no próprio job | Não: o CI não usa segredo da aplicação |
| **Staging e produção** | Um `.env` por ambiente na VPS, fora do git, com permissão `600` e dono o usuário de deploy, passado aos contêineres pelo `env_file` do compose | **Sim, e só aqui** |
| **Pipeline de deploy** | GitHub Secrets apenas com as credenciais de acesso à VPS (certificado do cliente OpenVPN, chave SSH). O push no GHCR usa o `GITHUB_TOKEN` do workflow | Credenciais de acesso, não da aplicação |

Consequência: **os segredos da aplicação existem num lugar só por ambiente**. Trocar um segredo é editar o `.env` da VPS e recriar o contêiner, sem build e sem deploy (fatores III e V).

## Desenvolvimento passo a passo

1. Copie o exemplo: `cp .env.example .env`. O `.env` está no `.gitignore` e nunca vai para o repositório.
2. Preencha os segredos:
   - `ConnectionStrings__APITechCurse` e `ConnectionStrings__RedisCache`, apontando para o seu PostgreSQL e o seu Redis;
   - `Jwt__SigningKey`, gerada na hora com `openssl rand -base64 48`;
   - opcionalmente `Seed__Admin__Email` e `Seed__Admin__Password`, para ter um Admin local (`AUTH-038`).
3. Rode a API com as variáveis do `.env`:
   - Git Bash, Linux ou macOS: `./scripts/com-env.sh` (sem argumentos, executa `dotnet run --project src/Api`);
   - PowerShell: `./scripts/com-env.ps1`;
   - IDE: aponte a run configuration para o arquivo `.env`.
4. Para outro comando com as mesmas variáveis, passe-o ao script, por exemplo: `./scripts/com-env.sh dotnet ef database update --project src/Infrastructure --startup-project src/Api`.

O formato do `.env` é uma variável por linha, `CHAVE=valor`, sem aspas; linhas vazias e linhas que começam com `#` são ignoradas.

## Proteções

- **A API falha no startup** quando falta configuração obrigatória (`TRV-023`), com uma mensagem que cita a chave e nunca o valor.
- **Configuração nunca vai para log nem para telemetria**, inclusive nos atributos do OpenTelemetry.
- **Segredo não aparece em linha de comando**: o comando de criação do primeiro Admin lê a senha de variável de ambiente (`TRV-035`).
- Se um dia for preciso endurecer, o passo seguinte é entregar os segredos como arquivos montados no contêiner (`secrets:` do compose, lidos pelo provedor `KeyPerFile` do .NET), sem mudar o resto desta estrutura.
