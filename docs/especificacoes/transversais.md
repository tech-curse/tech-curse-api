# Comportamentos transversais (`TRV`)

## Objetivo

Comportamentos que valem para toda a API, independentemente do recurso: formato das respostas de erro, correlação de requisições, CORS, limites de requisição, health checks, idempotência, cache de consultas e condições de inicialização.

## Regras

1. Todas as rotas de recursos ficam sob `/tech-curse/<Controller>`, com o nome do controller no singular (`/tech-curse/Course`). Health checks e Swagger ficam fora desse prefixo.
2. JSON em *camelCase*. Enums trafegam como texto (`"Student"`, `"CreditCard"`).
3. Todo erro de regra de negócio sai no formato `ProblemDetails` (`TRV-001`). Nenhuma resposta de erro expõe detalhes internos: mensagem de exceção inesperada, stack trace, nome de servidor ou endereço.
4. Toda resposta carrega um identificador de correlação, que também aparece nos logs daquela requisição.
5. A API só sobe com as dependências obrigatórias configuradas.
6. O que muda entre desenvolvimento, staging e produção é ligado por configuração explícita (`Payments:Enabled`, `Swagger:Enabled`), nunca pelo nome do ambiente. Cada chave tem o padrão seguro, que é desligado: produção funciona sem declarar nada. O nome do ambiente só aparece em travas de segurança que impedem um erro de configuração de chegar a produção (`TRV-033`, `AUTH-038`), e nunca como o único jeito de ligar ou desligar uma funcionalidade.
7. A API segue o [Twelve-Factor App](https://12factor.net/pt_br/). O checklist por fator, com o cenário ou a fase que resolve cada lacuna, está em [`docs/twelve-factor.md`](../twelve-factor.md).

## Contrato

### Formato de erro

```json
{
  "title": "UnprocessableEntity",
  "status": 422,
  "detail": "Ocorreram um ou mais erros de validação.",
  "instance": "/tech-curse/Course",
  "errors": { "Titulo": ["O título é obrigatório."] }
}
```

- `Content-Type: application/problem+json`.
- `title` é o nome do status HTTP em inglês (`NotFound`, `Conflict`, `UnprocessableEntity`...).
- `instance` é o caminho da requisição.
- `errors` só aparece em `422`: um dicionário de campo ou código de erro para a lista de mensagens.

| Exceção de domínio | Status |
| --- | --- |
| `BadRequestException` | `400` |
| `UnauthorizedException` | `401` |
| `ForbiddenAccessException` | `403` |
| `NotFoundException` | `404` |
| `ConflictException`, `NotAllowedException` | `409` |
| `ValidationException` | `422` |
| `GatewayTimeoutException` | `504` |
| qualquer outra | `500` |

### Cabeçalhos

| Cabeçalho | Direção | Significado |
| --- | --- | --- |
| `X-Correlation-ID` | requisição e resposta | Identificador de correlação (`TRV-005`) |
| `Retry-After` | resposta `429` | Segundos até a janela de limite reabrir |
| `Idempotency-Key` | requisição | Chave de idempotência das escritas de pagamento (`TRV-014`) |

## Cenários

### Erros

**TRV-001: Erros de domínio saem como ProblemDetails com o status mapeado**
*Quando* um handler lança uma exceção de domínio
*Então* a resposta tem o status da tabela acima, `Content-Type: application/problem+json`, `title`, `status`, `detail` (a mensagem da exceção) e `instance`
**Status:** implementado

**TRV-002: Erro de validação de negócio traz os campos com problema**
*Quando* um validator recusa a requisição
*Então* a resposta é `422`, com `detail` = `"Ocorreram um ou mais erros de validação."` e `errors` agrupado por nome de campo
**Status:** implementado

**TRV-003: Corpo malformado ou campo obrigatório ausente responde 400 no formato do ASP.NET Core**
*Quando* o JSON é inválido, ou falta um campo obrigatório do contrato
*Então* a resposta é `400`, no formato de validação padrão do ASP.NET Core (`title` = `"One or more validation errors occurred."`, `errors` por campo, `traceId`)
**Status:** implementado. Esse formato difere do `TRV-001` (não tem `instance`, tem `traceId`), e é aceito como está.

**TRV-004: Erro inesperado não vaza detalhes internos**
*Quando* acontece uma exceção que não é de domínio
*Então* a resposta é `500`, com `detail` = `"Ocorreu um erro inesperado. Informe o código de correlação ao suporte."` e o mesmo `X-Correlation-ID` registrado no log
*E* a exceção completa vai apenas para o log
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** o `detail` traz `exception.Message`, que pode conter mensagens do Npgsql, do Redis ou da biblioteca de JWT, com nome de servidor e detalhes de infraestrutura (ver `AUTH-027`).

### Correlação

**TRV-005: Toda resposta tem um identificador de correlação**
*Quando* a requisição não traz `X-Correlation-ID`
*Então* a resposta traz `X-Correlation-ID` com um GUID novo
*Quando* a requisição traz `X-Correlation-ID`
*Então* a resposta devolve o mesmo valor
*E* os logs da requisição têm a propriedade `CorrelationId` com esse valor
**Status:** implementado

**TRV-006: Identificador de correlação vindo de fora é validado**
*Quando* o `X-Correlation-ID` recebido tem mais de 64 caracteres, ou caracteres fora de letras, dígitos e hífen
*Então* a API o descarta e gera um GUID novo
**Status:** planejado (Fase 8, observabilidade). Hoje qualquer valor é aceito e vai para os logs como veio.

### CORS

**TRV-007: Só as origens configuradas acessam a API pelo navegador**
*Dado* `Cors:AllowedOrigins` = `"http://localhost:4200"`
*Quando* chega um preflight `OPTIONS` dessa origem
*Então* a resposta autoriza a origem, qualquer cabeçalho e qualquer método
*Quando* o preflight vem de outra origem
*Então* a resposta não traz `Access-Control-Allow-Origin`
**Status:** implementado

**TRV-008: Sem origens configuradas, nenhuma origem é liberada**
*Dado* `Cors:AllowedOrigins` vazio ou ausente
*Quando* chega qualquer requisição de outra origem
*Então* nenhuma resposta traz `Access-Control-Allow-Origin`; nunca há liberação geral (`*`)
**Status:** implementado

**TRV-009: Origens são normalizadas**
*Dado* `Cors:AllowedOrigins` = `" http://a.test/ , http://b.test "`
*Então* as duas origens funcionam, sem a barra final e sem espaços
**Status:** implementado

**TRV-010: O navegador consegue ler os cabeçalhos de correlação e de limite**
*Quando* uma origem permitida recebe uma resposta real (não o preflight)
*Então* `Access-Control-Expose-Headers` inclui `X-Correlation-ID` e `Retry-After`
*E* `Access-Control-Allow-Credentials` não está presente
**Status:** implementado. A Fase 5 muda isso para permitir o cookie do refresh token.

**TRV-011: Preflight não é bloqueado por redirect nem por rate limiting**
*Quando* um preflight chega por HTTP, ou quando o cliente já estourou o limite de requisições
*Então* o preflight ainda recebe os cabeçalhos de CORS, sem `307` nem `429`
**Status:** implementado

### Limites de requisição

**TRV-012: Limite global por usuário ou por IP**
*Dado* um cliente autenticado
*Quando* ele faz mais de `RateLimiting:GlobalPermitLimit` (padrão `200`) requisições em `RateLimiting:GlobalWindowSeconds` (padrão `60`) segundos
*Então* as excedentes recebem `429` em `ProblemDetails`, com `detail` = `"Muitas requisições em um curto intervalo. Tente novamente mais tarde."` e o cabeçalho `Retry-After`
*E* o limite é contado por usuário; para anônimos, por IP
**Status:** implementado

**TRV-013: Atrás do proxy reverso, o limite usa o IP real do cliente**
*Dado* a API atrás do Nginx do host
*Quando* duas pessoas diferentes fazem requisições anônimas
*Então* cada uma é contada no próprio limite, pelo IP de origem informado pelo proxy em `X-Forwarded-For`
**Status:** planejado (Fase 6, ambientes). Sem o tratamento de cabeçalhos encaminhados, todas as requisições chegam com o IP do Nginx: o limite de autenticação (10 por minuto) passaria a valer para todos os usuários juntos.

`RateLimiting:Enabled` = `false` desliga os dois limites. Isso existe para os testes e nunca é usado em ambiente real.

O estado dos limites fica **na memória de cada processo**, por decisão: a API roda com uma réplica só. Com mais de uma réplica, cada uma teria o próprio contador e o limite efetivo se multiplicaria. Subir uma segunda réplica exige rever esta decisão (exceção registrada no fator VI do `docs/twelve-factor.md`).

### Idempotência

**TRV-014: Escritas idempotentes exigem a chave**
*Quando* uma escrita marcada como idempotente chega sem `Idempotency-Key`
*Então* a resposta é `400`, com `detail` = `"O header 'Idempotency-Key' é obrigatório para requisições idempotentes."`
**Status:** implementado

**TRV-015: A mesma chave devolve a mesma resposta, sem executar de novo**
*Dado* uma escrita idempotente concluída com sucesso
*Quando* o mesmo usuário repete a requisição com a mesma `Idempotency-Key` em até 6 minutos
*Então* a resposta tem o mesmo status e o mesmo corpo da primeira
*E* a operação não é executada de novo
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** pela leitura do código, a repetição falha. O filtro serializa a resposta para texto e o `RedisCacheService.SetAsync` serializa de novo. Na leitura, o JSON guardado é uma string, que não pode ser convertida no modelo da resposta: a repetição deve terminar em `500`. O primeiro teste vai confirmar.

**TRV-016: A chave vale só para o mesmo endpoint**
*Dado* uma chave usada em `POST /Payment`
*Quando* a mesma chave é enviada em `POST /Payment/process`
*Então* o segundo endpoint executa normalmente, sem devolver a resposta do primeiro
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** a chave do cache é só o valor do cabeçalho (separada por usuário, mas não por endpoint).

**TRV-017: Erro não é guardado como resposta idempotente**
*Quando* a primeira execução termina em erro (`4xx` ou `5xx`)
*Então* uma repetição com a mesma chave executa a operação de novo
**Status:** implementado

**TRV-018: Mesma chave com corpo diferente é recusada**
*Quando* a chave já usada chega com um corpo diferente do original
*Então* a resposta é `422`, sem executar a operação
**Status:** planejado (junto com a volta dos pagamentos, issue #1)

### Cache de consultas

**TRV-019: Escritas invalidam o cache de todos os usuários afetados**
*Dado* um aluno que consultou os próprios pagamentos ou o catálogo (respostas guardadas em cache)
*Quando* um Admin processa um pagamento desse aluno, ou edita ou remove um curso
*Então* a próxima consulta do aluno já mostra o dado novo
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** as chaves de cache levam o id do usuário que consultou, e a invalidação apaga só as chaves de quem fez a escrita. O aluno continua vendo o status antigo por até 15 minutos, o prazo usado pelas consultas. O mesmo vale para cursos: um curso editado ou removido por um Admin continua aparecendo como antes para os alunos que já tinham consultado. Detalhes em `cursos.md` e `pagamentos.md`.

### Paginação

As listagens paginadas (cursos, alunos, pagamentos) recebem pela query string `PageNumber` (padrão `1`), `PageSize` (padrão `10`), `SortBy` (padrão `Id`) e `SortDirection` (`asc`, o padrão, ou `desc`), e respondem:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

**TRV-027: Página e tamanho padrão**
*Quando* a listagem é chamada sem parâmetros
*Então* a resposta traz a página `1`, com até `10` itens, na ordem padrão da listagem: id crescente para cursos e alunos; mais recente primeiro para pagamentos (ver `pagamentos.md`)
**Status:** implementado

**TRV-028: Tamanho de página tem teto**
*Quando* `PageSize` é maior que `50`
*Então* a resposta usa `50`
**Status:** implementado

**TRV-029: Os metadados descrevem a página corretamente**
*Dado* 25 registros e `PageSize` = `10`
*Quando* a página `3` é pedida
*Então* `items` tem 5 itens, `totalCount` = `25`, `totalPages` = `3`, `hasPreviousPage` = `true` e `hasNextPage` = `false`
**Status:** implementado

**TRV-030: Página além da última vem vazia**
*Quando* `PageNumber` é maior que `totalPages`
*Então* a resposta é `200`, com `items` vazio e os metadados corretos
**Status:** implementado

**TRV-031: Página ou tamanho inválido é recusado**
*Quando* `PageNumber` ou `PageSize` é menor que `1`
*Então* a resposta é `422`, com `errors` apontando o parâmetro inválido
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** `PageNumber` = `0` gera um `OFFSET` negativo, que o PostgreSQL recusa: `500`. `PageSize` = `0` divide por zero no cálculo de `totalPages`, que, havendo registros, sai como `2147483647`, com `hasNextPage` = `true`. O catálogo do web troca uma página inválida da URL por `1`, então o `500` aparece só para quem chama a API diretamente.

**TRV-032: Ordenação desconhecida cai no padrão**
*Quando* `SortBy` não é um dos campos aceitos pela listagem
*Então* a listagem usa a ordem padrão dela, na direção pedida, sem erro (as de pagamento ignoram `SortBy` e `SortDirection`)
**Status:** implementado

### Health checks

**TRV-020: Liveness responde sem depender de nada**
*Quando* chega `GET /health/live`
*Então* a resposta é `200` com o texto `Healthy`, mesmo com PostgreSQL ou Redis fora do ar
**Status:** implementado

**TRV-021: Readiness reflete as dependências**
*Quando* chega `GET /health/ready`
*Então* a resposta é `200` com `{ "status": "Healthy" }` se PostgreSQL (`Database_Postgres`) e Redis (`Cache_Redis`) respondem
*E* `503` com `{ "status": "Unhealthy" }` se algum deles não responde
**Status:** implementado

**TRV-022: Só Admin vê o detalhe por dependência**
*Quando* `GET /health/ready` chega sem token, ou com token que não é de `Admin`
*Então* o corpo tem só `status`
*Quando* chega com token de `Admin`
*Então* o corpo tem também `duracaoMs` e `checks`, com `nome`, `status`, `duracaoMs`, `descricao` e `erro` de cada dependência
**Status:** implementado

### Inicialização

**TRV-023: Sem configuração obrigatória, a API não sobe**
*Quando* falta `ConnectionStrings:APITechCurse`, `ConnectionStrings:RedisCache` ou uma `Jwt:SigningKey` válida (`AUTH-035`)
*Então* a aplicação falha no startup, com uma mensagem que diz qual configuração falta
**Status:** implementado

**TRV-024: Migrations rodam como etapa do deploy, não no startup**
*Quando* a API sobe
*Então* ela não altera o schema do banco; as migrations são aplicadas por uma etapa explícita do deploy, antes da nova versão entrar no ar
**Status:** planejado (Fase 7). Hoje o `Program.cs` aplica as migrations no startup e, se falharem, a API não sobe.

**TRV-025: O módulo de pagamentos é ligado por configuração**
*Quando* a API sobe com `Payments:Enabled` = `false` ou sem a chave
*Então* ela sobe e responde normalmente, as rotas de `/tech-curse/Payment` respondem `404` e não aparecem no Swagger
*Quando* sobe com `Payments:Enabled` = `true`
*Então* as rotas de pagamento estão disponíveis
**Status:** planejado (antes do primeiro deploy em produção; issue #1). Hoje a chave não existe: as rotas de pagamento estão sempre registradas, e a API se recusa a subir em `Production` (ver `TRV-033`).

**TRV-026: Swagger ligado por configuração**
*Quando* `Swagger:Enabled` = `true`
*Então* `/swagger` está disponível
*Quando* `Swagger:Enabled` = `false` ou a chave não existe
*Então* `/swagger` responde `404`
*E* desenvolvimento e staging ligam a chave; produção não a declara
**Status:** divergente: correção proposta para a Fase 6 (ambientes)
**Hoje:** o Swagger aparece quando o ambiente se chama `Development` ou `Homolog`. Um staging com outro nome, como o `Staging` previsto para a Fase 6, fica sem Swagger.

**TRV-033: Gateway simulado nunca em produção**
*Dado* que o único gateway de pagamento disponível é o simulado
*Quando* a API sobe em `Production` com `Payments:Enabled` = `true`
*Então* a aplicação falha no startup, com uma mensagem que diz que não há gateway real configurado
*Quando* sobe em `Production` com os pagamentos desligados
*Então* sobe normalmente (`TRV-025`)
**Status:** planejado (junto com `TRV-025`; issue #1). Hoje a trava derruba a API em `Production` mesmo com os pagamentos desligados, porque a chave ainda não existe. A trava olha o nome do ambiente de propósito: é a segunda barreira, para o caso de a chave ser ligada por engano em produção, e um gateway que fabrica aprovações confirmaria cobranças que nunca aconteceram.

### Configuração e processos (Twelve-Factor)

**TRV-034: Configuração só por variáveis de ambiente**
*Dado* qualquer ambiente, inclusive o de desenvolvimento
*Então* a API lê a configuração de `appsettings.json`, que só tem padrões seguros, iguais em todo ambiente, e de variáveis de ambiente, que prevalecem
*E* não existe `appsettings.<Ambiente>.json` nem User Secrets
*E* em desenvolvimento, um `.env` local fora do git, copiado do `.env.example`, fornece as variáveis ao compose de desenvolvimento e ao `dotnet run`
**Status:** divergente: correção proposta para a Fase 4, junto com o compose de desenvolvimento
**Hoje:** o `appsettings.Development.json` traz emissor, audiência e origem do CORS de desenvolvimento; o `TechCurse.Api.csproj` tem `UserSecretsId`; e o README e o `CLAUDE.md` orientam gravar connection strings e a chave de assinatura em User Secrets.

**TRV-035: O primeiro Admin é criado por um processo administrativo avulso**
*Dado* a imagem da release, em qualquer ambiente
*Quando* o operador roda o comando avulso de criação de Admin, com o e-mail e a senha em variáveis de ambiente
*Então* o Admin é criado, e o processo termina com código `0`
*E* se já existe um usuário com esse e-mail, o comando não o altera, informa isso e termina com código `0`
*E* se a senha não atende à política (`AUTH-004`), o comando termina com código diferente de `0` e diz o motivo
*E* a senha nunca é aceita como argumento de linha de comando nem aparece em log
**Status:** planejado (antes do primeiro deploy em produção; Fase 7). Hoje o único caminho é o seed de `Development` (`AUTH-038`), então uma produção nova não teria nenhum Admin para criar cursos e instrutores.

**TRV-036: Encerramento limpo**
*Quando* o processo recebe `SIGTERM`
*Então* ele deixa de aceitar conexões novas, conclui as requisições em andamento em até 25 segundos e termina com código `0`
*E* o orquestrador espera pelo menos 30 segundos antes de matar o processo
**Status:** planejado (Fase 4). Hoje o ASP.NET Core já espera até 30 segundos pelas requisições em andamento, mas o `docker stop` mata o processo em 10 segundos, o padrão do Docker.

**TRV-037: HTTP na porta configurada; TLS fica no proxy**
*Então* a API atende HTTP na porta de `ASPNETCORE_HTTP_PORTS` (`8080` na imagem) e não redireciona para HTTPS
*E* o esquema e o IP originais vêm de `X-Forwarded-Proto` e `X-Forwarded-For`, aceitos só do proxy reverso (`TRV-013`)
**Status:** divergente: correção proposta para a Fase 6
**Hoje:** `UseHttpsRedirection()` está no pipeline. Atrás do Nginx, que termina o TLS, a API não tem como saber que a conexão original era HTTPS.

**TRV-038: Telemetria com OpenTelemetry**
*Então* os logs estruturados vão sempre para o stdout, em JSON, um evento por linha
*E* quando `OTEL_EXPORTER_OTLP_ENDPOINT` está configurado, a API exporta logs, traces e métricas via OTLP para esse endereço, com `service.name` = `tech-curse-api`, `service.version` = versão da release e o ambiente em `deployment.environment` (via `OTEL_RESOURCE_ATTRIBUTES`)
*E* sem essa variável, nada é exportado e a API funciona normalmente
*E* cada log emitido durante uma requisição traz o `TraceId` e o `SpanId` dela, para ir do log ao trace no Grafana
*E* a API não conhece Loki, Tempo nem Mimir: o coletor OpenTelemetry decide o destino de cada sinal
**Status:** planejado (Fase 8). Hoje só existe o log JSON no stdout (Serilog).

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `TRV-004` | `500` devolve a mensagem interna da exceção | Mensagem genérica com o código de correlação; detalhe só no log | Fase 3 |
| `TRV-015` | Repetição com a mesma chave deve dar `500` (dupla serialização) | Guardar e ler o modelo sem a serialização extra | Fase 3 |
| `TRV-016` | Chave de idempotência não separa endpoints | Incluir método e rota na chave | Fase 3 |
| `TRV-019` | Escrita só limpa o cache de quem escreveu; os outros veem dado antigo por até 15 minutos | Dados compartilhados (catálogo, pagamentos) com chave sem usuário, invalidada por todos. A checagem de permissão ("é o próprio aluno?") acontece sempre **antes** de ler o cache, porque hoje é a chave por usuário que isola os dados de cada um | Fase 3 |
| `TRV-031` | Página `0` dá `500`; tamanho `0` gera `totalPages` sem sentido | `422` para página ou tamanho menor que `1` | Fase 3 |
| `TRV-013` | Limite por IP vê só o IP do proxy | Tratar `X-Forwarded-For` vindo do Nginx | Fase 6 |
| `TRV-024` | Migrations no startup | Etapa explícita do deploy | Fase 7 |
| `TRV-025` | API não sobe em `Production`; pagamentos sempre registrados | `Payments:Enabled`, desligado por padrão | Antes do 1º deploy |
| `TRV-026` | Swagger decidido pelos nomes de ambiente `Development` e `Homolog` | `Swagger:Enabled`, desligado por padrão | Fase 6 |
| `TRV-033` | Trava derruba a API em `Production` mesmo sem pagamentos | Trava só quando `Payments:Enabled` = `true` com o gateway simulado | Antes do 1º deploy |
| `TRV-034` | `appsettings.Development.json`, User Secrets | Só `appsettings.json` com padrões seguros e variáveis de ambiente; `.env` local em dev | Fase 4 |
| `TRV-035` | Sem forma de criar o primeiro Admin fora de `Development` | Comando avulso na imagem da release | Antes do 1º deploy |
| `TRV-036` | `docker stop` mata em 10 s; a API espera até 30 s | Encerrar em até 25 s; orquestrador espera 30 s | Fase 4 |
| `TRV-037` | `UseHttpsRedirection()` atrás do proxy | HTTP puro; esquema e IP via `X-Forwarded-*` do proxy | Fase 6 |
| `TRV-038` | Só log JSON no stdout | Logs também, mais traces e métricas, via OTLP quando configurado | Fase 8 |

## Fora de escopo

- Versionamento da API na URL (`/v2/...`): o único consumidor é o próprio front-end.
- Compressão de respostas: feita pelo Nginx.
