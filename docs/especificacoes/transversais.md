# Comportamentos transversais (`TRV`)

## Objetivo

Comportamentos que valem para toda a API, independentemente do recurso: formato das respostas de erro, correlação de requisições, CORS, limites de requisição, health checks, idempotência, cache de consultas e condições de inicialização.

## Regras

1. Todas as rotas de recursos ficam sob `/tech-curse/<Controller>`, com o nome do controller no singular (`/tech-curse/Course`). Health checks e Swagger ficam fora desse prefixo.
2. JSON em *camelCase*. Enums trafegam como texto (`"Student"`, `"CreditCard"`).
3. Todo erro de regra de negócio sai no formato `ProblemDetails` (`TRV-001`). Nenhuma resposta de erro expõe detalhes internos: mensagem de exceção inesperada, stack trace, nome de servidor ou endereço.
4. Toda resposta carrega um identificador de correlação, que também aparece nos logs daquela requisição.
5. A API só sobe com as dependências obrigatórias configuradas.

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
**Hoje:** `PageNumber` = `0` gera um `OFFSET` negativo, que o PostgreSQL recusa: `500`. `PageSize` = `0` divide por zero no cálculo de `totalPages`, que, havendo registros, sai como `2147483647`, com `hasNextPage` = `true`. O web lê a página da URL, então `?pagina=0` no navegador já provoca o `500`.

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

**TRV-025: Produção sobe sem o módulo de pagamentos**
*Quando* a API sobe com `ASPNETCORE_ENVIRONMENT=Production`
*Então* ela sobe e responde normalmente, e os endpoints de pagamento não estão disponíveis
**Status:** planejado (antes do primeiro deploy em produção; issue #1). Hoje a API se recusa a subir em `Production`, porque o único gateway de pagamento é simulado.

**TRV-026: Swagger só fora de produção**
*Quando* a API roda em `Development` ou em staging
*Então* `/swagger` está disponível
*Quando* roda em produção
*Então* `/swagger` responde `404`
**Status:** implementado com o nome de ambiente `Homolog`; a Fase 6 troca para `Staging`.

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
| `TRV-025` | API não sobe em `Production` | Subir sem o módulo de pagamentos | Antes do 1º deploy |

## Fora de escopo

- Versionamento da API na URL (`/v2/...`): o único consumidor é o próprio front-end.
- Compressão de respostas: feita pelo Nginx.
