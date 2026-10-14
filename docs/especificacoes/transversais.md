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
**Status:** implementado

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
**Status:** implementado

**TRV-040: Recusa esperada não é registrada como erro**
*Quando* a resposta vem de uma exceção de domínio com status `4xx` (`400`, `401`, `403`, `404`, `409`, `422`)
*Então* o log é um aviso (`Warning`) com o status e o motivo, sem a pilha de chamadas
*Quando* é um `500` ou um `504`
*Então* o log é um erro (`Error`) com a exceção completa
*E* o SQL executado pelo EF Core não é registrado no nível `Information`
**Status:** implementado. Alertas olham o nível `Error`: uma tentativa de login sem sessão não pode acordar ninguém.

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
**Status:** implementado. O cookie da sessão (`AUTH-034`) não muda isso: o front e a API ficam na mesma origem em todo ambiente, e o cookie nunca atravessa origens.

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
*E* `X-Forwarded-For` só é aceito de quem está em `ForwardedHeaders:KnownProxies` (IPs) ou `ForwardedHeaders:KnownNetworks` (redes CIDR), além do loopback; de qualquer outro remetente o cabeçalho é ignorado, para um cliente não escolher o próprio IP
*E* um valor inválido nessas chaves impede a API de subir, com a chave e o valor na mensagem
**Status:** implementado. Sem isso, todas as requisições chegariam com o IP do Nginx, e o limite de autenticação (10 por minuto) valeria para todos os usuários juntos.

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
**Status:** implementado

**TRV-016: A chave vale só para o mesmo endpoint**
*Dado* uma chave usada em `POST /Payment`
*Quando* a mesma chave é enviada em `POST /Payment/process`
*Então* o segundo endpoint executa normalmente, sem devolver a resposta do primeiro
**Status:** implementado

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
**Status:** implementado

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
**Status:** implementado

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
**Status:** implementado. A etapa é o comando avulso `migrar` (`ComandosAdministrativos`), que aplica as migrations e cria as roles; no compose de desenvolvimento, o serviço `migrar` roda antes da API.

**TRV-025: O módulo de pagamentos é ligado por configuração**
*Quando* a API sobe com `Payments:Enabled` = `false` ou sem a chave
*Então* ela sobe e responde normalmente, as rotas de `/tech-curse/Payment` respondem `404` e não aparecem no Swagger
*Quando* sobe com `Payments:Enabled` = `true`
*Então* as rotas de pagamento estão disponíveis
**Status:** implementado. Com a chave desligada, o `PaymentController` sai da aplicação (`PagamentosSetup`): as rotas não existem para o roteamento nem para o Swagger. O `.env.example` de desenvolvimento liga a chave.

**TRV-026: Swagger ligado por configuração**
*Quando* `Swagger:Enabled` = `true`
*Então* `/swagger` está disponível
*Quando* `Swagger:Enabled` = `false` ou a chave não existe
*Então* `/swagger` responde `404`
*E* desenvolvimento e staging ligam a chave; produção não a declara
*E* o nome do ambiente não liga o Swagger: em `Development`, sem a chave, ele também responde `404`
**Status:** implementado

**TRV-033: Gateway simulado nunca em produção**
*Dado* que o único gateway de pagamento disponível é o simulado
*Quando* a API sobe em `Production` com `Payments:Enabled` = `true`
*Então* a aplicação falha no startup, com uma mensagem que diz que não há gateway real configurado
*Quando* sobe em `Production` com os pagamentos desligados
*Então* sobe normalmente (`TRV-025`)
**Status:** implementado. Em `Production` o gateway simulado nunca é registrado, com a chave ligada ou não. A trava olha o nome do ambiente de propósito: é a segunda barreira, para o caso de a chave ser ligada por engano em produção, e um gateway que fabrica aprovações confirmaria cobranças que nunca aconteceram.

### Configuração e processos (Twelve-Factor)

**TRV-034: A API lê configuração só de `appsettings.json` e de variáveis de ambiente**
*Dado* qualquer ambiente, inclusive o de desenvolvimento
*Então* a API lê a configuração de `appsettings.json`, que só tem padrões seguros, iguais em todo ambiente, e de variáveis de ambiente, que prevalecem
*E* não existe `appsettings.<Ambiente>.json` nem User Secrets
*E* a API não lê nenhum arquivo `.env`: quem inicia o processo (IDE, script, compose, workflow do CI) transforma o `.env` em variáveis de ambiente
*E* toda chave que a API lê está no `.env.example`: os segredos sem valor; o resto com um valor de exemplo de desenvolvimento
*E* a estrutura completa (o que mora onde, em cada contexto) está em [`docs/configuracao.md`](../configuracao.md)
**Status:** implementado. O compose de desenvolvimento lê o mesmo `.env`.

**TRV-039: Segredos da aplicação existem só no ambiente que os usa**
*Então* os segredos de staging e de produção ficam apenas no `.env` de cada ambiente na VPS (fora do git, permissão `600`, dono o usuário de deploy)
*E* o pipeline de deploy não conhece nenhum segredo da aplicação: só as credenciais para alcançar a VPS
*E* o CI não usa segredo da aplicação: os testes geram valores descartáveis no próprio job
*E* trocar um segredo é editar o `.env` da VPS e recriar o contêiner, sem build nem deploy
**Status:** planejado (Fases 6 e 7)

**TRV-035: O primeiro Admin é criado por um processo administrativo avulso**
*Dado* a imagem da release, em qualquer ambiente
*Quando* o operador roda o comando avulso de criação de Admin, com o e-mail e a senha em variáveis de ambiente
*Então* o Admin é criado, e o processo termina com código `0`
*E* se já existe um usuário com esse e-mail, o comando não o altera, informa isso e termina com código `0`
*E* se a senha não atende à política (`AUTH-004`), o comando termina com código diferente de `0` e diz o motivo
*E* a senha nunca é aceita como argumento de linha de comando nem aparece em log
**Status:** implementado. O comando é `criar-admin`, com `CriarAdmin__Email` e `CriarAdmin__Password`; argumento extra é recusado.

**TRV-036: Encerramento limpo**
*Quando* o processo recebe `SIGTERM`
*Então* ele deixa de aceitar conexões novas, conclui as requisições em andamento em até 25 segundos e termina com código `0`
*E* o orquestrador espera pelo menos 30 segundos antes de matar o processo
**Status:** implementado. A espera fica em `HostOptions.ShutdownTimeout` (`EncerramentoSetup`), e o `stop_grace_period` do compose é de 30 segundos; o padrão do Docker, 10 segundos, mataria o processo antes.

**TRV-037: HTTP na porta configurada; TLS fica no proxy**
*Então* a API atende HTTP na porta de `ASPNETCORE_HTTP_PORTS` (`8080` na imagem) e não redireciona para HTTPS
*E* o esquema e o IP originais vêm de `X-Forwarded-Proto` e `X-Forwarded-For`, aceitos só do proxy reverso (`TRV-013`)
**Status:** implementado. O `UseHttpsRedirection()` saiu do pipeline, e o `UseForwardedHeaders()` é o primeiro middleware.

**TRV-038: Telemetria com OpenTelemetry**
*Então* os logs estruturados vão sempre para o stdout, em JSON, um evento por linha
*E* quando `OTEL_EXPORTER_OTLP_ENDPOINT` está configurado, a API exporta logs, traces e métricas via OTLP para esse endereço, com `service.name` = `tech-curse-api`, `service.version` = versão da release e o ambiente em `deployment.environment` (via `OTEL_RESOURCE_ATTRIBUTES`)
*E* sem essa variável, nada é exportado e a API funciona normalmente
*E* cada log emitido durante uma requisição traz o `TraceId` e o `SpanId` dela, para ir do log ao trace no Grafana
*E* a API não conhece Loki, Tempo nem Mimir: o coletor OpenTelemetry decide o destino de cada sinal
**Status:** implementado (`TelemetriaSetup`). Os traces cobrem as requisições, o PostgreSQL (Npgsql) e o HTTP de saída; as sondas de `/health` não geram trace. O Redis fica sem instrumentação, porque o pacote ainda não tem versão estável. O log do stdout leva o `TraceId` e o `SpanId` pelo Serilog; o exportado via OTLP, pelo SDK.

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `TRV-039` | Segredos ainda sem lugar definido fora do desenvolvimento | Só no `.env` de cada ambiente na VPS; pipeline e CI sem segredos da aplicação | Fases 6 e 7 |

## Fora de escopo

- Versionamento da API na URL (`/v2/...`): o único consumidor é o próprio front-end.
- Compressão de respostas: feita pelo Nginx.
