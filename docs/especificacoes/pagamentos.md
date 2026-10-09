# Pagamentos (`PAG`)

## Objetivo

Registrar a cobrança de uma matrícula, processá-la num gateway de pagamento e estorná-la, mantendo o histórico financeiro mesmo quando o aluno é removido.

> **Situação do módulo.** O único gateway disponível é **simulado** (`SimulatedPaymentGatewayAdapter`): ele fabrica aprovações e não movimenta dinheiro. Por isso o módulo de pagamentos fica fora de produção até existir um gateway real ([issue #1](https://github.com/tech-curse/tech-curse-api/issues/1); ver `TRV-025`). Os cenários abaixo valem para desenvolvimento e staging.

## Regras

1. Um pagamento pertence a uma matrícula ativa. O aluno do pagamento é o aluno da matrícula.
2. Uma matrícula tem no máximo **um pagamento ativo**. Um pagamento estornado fica inativo e libera a matrícula para um pagamento novo.
3. Ciclo de vida: `Pending` → `Paid` (processado) → `Refunded` (estornado). Uma recusa do gateway mantém o pagamento em `Pending`, e ele pode ser processado de novo.
4. O valor é informado pelo `Admin` (o curso não tem preço cadastrado): maior que zero, com até duas casas decimais.
5. Só `Admin` cria, processa e estorna. O aluno consulta os próprios pagamentos; o `Admin` consulta todos.
6. Criar, processar e estornar são escritas idempotentes e exigem `Idempotency-Key` (`TRV-014` a `TRV-018`).
7. A chamada ao gateway tem limite de 30 segundos.
8. O histórico financeiro sobrevive à remoção do aluno: os pagamentos de um aluno removido continuam visíveis para o `Admin`.

## Contrato HTTP

Rotas sob `/tech-curse/Payment`.

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `POST /` | `Admin` | `{ enrollmentId, amount }` | `201` pagamento, com `Location` |
| `POST /process` | `Admin` | `{ paymentId, type }` | `200` `{ success, message, externalTransactionId }` |
| `POST /refund` | `Admin` | `{ paymentId, reason }` | `200` `{ success, message }` |
| `GET /` | `Admin` | query: paginação | `200` lista paginada |
| `GET /{id}` | dono ou `Admin` | | `200` pagamento |
| `GET /student/{studentId}` | o próprio aluno ou `Admin` | query: paginação | `200` lista paginada |
| `GET /enrollment/{enrollmentId}` | dono da matrícula ou `Admin` | | `200` lista |

- Pagamento: `{ paymentId, enrollmentId, studentId, amount, status, isActive, createdAt, paidAt, externalTransactionId, courseId, courseTitulo }`.
- `status`: `"Pending"`, `"Paid"`, `"Failed"` ou `"Refunded"`. `"Failed"` existe no contrato, mas hoje nenhum fluxo o produz.
- `type`: `"CreditCard"`, `"Pix"` ou `"Boleto"`; só `"CreditCard"` é processado (`PAG-011`).
- As listagens vêm sempre do pagamento mais recente para o mais antigo (`createdAt` decrescente); `SortBy` e `SortDirection` são ignorados.

### Gateway simulado

Para permitir testar recusas, o gateway simulado decide pelo valor:

| Valor | Resultado | Código |
| --- | --- | --- |
| `999.99` | recusa por antifraude | `FRAUD_DETECTED` |
| `500.00` | recusa por saldo insuficiente | `INSUFFICIENT_FUNDS` |
| qualquer outro | aprovado, com `externalTransactionId` = `sim_<guid>` | |

## Cenários

### Criação

**PAG-001: Admin cria um pagamento para uma matrícula ativa**
*Dado* uma matrícula ativa sem pagamento ativo
*Quando* um `Admin` envia `POST /Payment` com `enrollmentId`, `amount` e `Idempotency-Key`
*Então* a resposta é `201`, com o pagamento em `status` = `"Pending"`, `isActive` = `true`, o `studentId` da matrícula e o curso da matrícula
*E* o cabeçalho `Location` aponta para `/tech-curse/Payment/{id}`
**Status:** implementado

**PAG-002: Matrícula inexistente**
*Quando* o `enrollmentId` não existe
*Então* a resposta é `404`, com `detail` = `"Matrícula não encontrada."`
**Status:** implementado

**PAG-003: Uma matrícula, um pagamento ativo**
*Dado* uma matrícula que já tem um pagamento ativo (pendente ou pago)
*Quando* um `Admin` tenta criar outro pagamento para ela
*Então* a resposta é `409`, com `detail` = `"Já existe um pagamento ativo para esta matrícula."`
**Status:** implementado

**PAG-004: Duas criações simultâneas não geram dois pagamentos ativos**
*Dado* uma matrícula sem pagamento ativo
*Quando* duas criações para ela chegam ao mesmo tempo
*Então* uma resposta é `201` e a outra `409`, com a mesma mensagem do `PAG-003`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** o banco tem um índice único parcial (`"IsActive" = true`) que impede o segundo pagamento ativo. A violação, porém, chega como `DbUpdateException`, que vira `500` com a mensagem interna do PostgreSQL (`TRV-004`).

**PAG-005: Dados inválidos são recusados**
*Quando* `enrollmentId` ou `amount` é zero ou negativo
*Então* a resposta é `422`, com `errors.EnrollmentId` = `["O ID da matrícula deve ser maior que zero."]` ou `errors.Amount` = `["O valor do pagamento deve ser maior que zero."]`
**Status:** implementado

**PAG-006: Só Admin cria, processa e estorna**
*Quando* um `Student` ou um `Instructor` chama `POST /Payment`, `POST /Payment/process` ou `POST /Payment/refund`
*Então* a resposta é `403`
**Status:** implementado

### Processamento

**PAG-007: Processar com cartão de crédito aprova o pagamento**
*Dado* um pagamento `Pending` e ativo, com valor fora da tabela de recusas
*Quando* um `Admin` envia `POST /Payment/process` com `{ paymentId, "type": "CreditCard" }` e uma `Idempotency-Key`
*Então* a resposta é `200`, com `success` = `true`, `message` = `"Pagamento processado com sucesso."` e o `externalTransactionId`
*E* o pagamento passa a `status` = `"Paid"`, com `paidAt` (UTC) e `externalTransactionId` preenchidos
**Status:** implementado

**PAG-008: Só pagamento pendente e ativo é processado**
*Dado* um pagamento `Paid` ou `Refunded`
*Quando* um `Admin` tenta processá-lo
*Então* a resposta é `409`, com `detail` = `"O pagamento não está em um estado válido para processamento."`, e nada muda
**Status:** implementado

**PAG-009: Recusa do gateway mantém o pagamento pendente**
*Dado* um pagamento `Pending` de valor `999.99` (ou `500.00`)
*Quando* um `Admin` o processa
*Então* a resposta é `400`, com `detail` = `"Falha ao processar pagamento [FRAUD_DETECTED]: Transação recusada pelo sistema antifraude."` (ou `[INSUFFICIENT_FUNDS]: Saldo insuficiente no método de pagamento.`)
*E* o pagamento continua `Pending` e ativo, e pode ser processado de novo
**Status:** implementado

**PAG-010: Pagamento inexistente**
*Quando* `POST /Payment/process` ou `POST /Payment/refund` usa um `paymentId` que não existe
*Então* a resposta é `404`, com `detail` = `"Pagamento não encontrado."`
**Status:** implementado

**PAG-011: Método de pagamento sem suporte é recusado com 422**
*Quando* o `type` é `"Pix"` ou `"Boleto"`
*Então* a resposta é `422`, com `errors.Type` = `["Método de pagamento não suportado."]`, e o pagamento não muda
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** os dois valores passam pela validação (são do enum), mas não há estratégia registrada para eles. A factory lança `NotSupportedException`, e a resposta é `500`, com `"Método de pagamento Pix não suportado."` no `detail`.

**PAG-012: Gateway lento gera 504**
*Dado* um gateway que não responde em 30 segundos
*Quando* um `Admin` processa ou estorna um pagamento
*Então* a resposta é `504`, e o pagamento não muda
**Status:** implementado

**PAG-013: O mesmo pagamento não é cobrado duas vezes**
*Dado* um pagamento `Pending`
*Quando* duas requisições de processamento com chaves de idempotência diferentes chegam ao mesmo tempo
*Então* o gateway é chamado uma vez só; a segunda requisição recebe `409`
**Status:** planejado (com o gateway real, issue #1). Hoje as duas leem o pagamento como `Pending` e as duas chamam o gateway. Com o gateway simulado não há prejuízo; com um real, haveria cobrança dupla.

### Estorno

**PAG-014: Admin estorna um pagamento pago**
*Dado* um pagamento `Paid`
*Quando* um `Admin` envia `POST /Payment/refund` com `paymentId` e uma `Idempotency-Key`
*Então* a resposta é `200`, com `success` = `true` e `message` = `"Estorno realizado com sucesso."`
*E* o pagamento passa a `status` = `"Refunded"` e `isActive` = `false`
*E* a matrícula aceita um pagamento novo (`PAG-001`)
**Status:** implementado

**PAG-015: Só pagamento pago é estornado**
*Dado* um pagamento `Pending` ou `Refunded`
*Quando* um `Admin` tenta estorná-lo
*Então* a resposta é `409`, com `detail` = `"Apenas pagamentos processados e com ID de transação podem ser estornados."`
**Status:** implementado

**PAG-016: O motivo do estorno fica registrado**
*Quando* o estorno é feito com `reason`
*Então* o motivo fica guardado no pagamento e aparece nas consultas
**Status:** planejado (issue #1). Hoje `reason` é aceito no corpo e descartado.

### Consultas

**PAG-017: Admin lista todos os pagamentos**
*Quando* um `Admin` chama `GET /Payment`
*Então* a resposta é `200`, com a lista paginada de todos os pagamentos, do mais recente para o mais antigo, inclusive os de alunos removidos
*Quando* um `Student` ou um `Instructor` chama
*Então* a resposta é `403`
**Status:** implementado

**PAG-018: Aluno consulta os próprios pagamentos**
*Dado* um aluno com pagamentos
*Quando* ele chama `GET /Payment/student/{studentId}` com o próprio id
*Então* a resposta é `200`, com a lista paginada dos pagamentos dele, do mais recente para o mais antigo
*E* o `Admin` obtém o mesmo resultado para qualquer aluno ativo
**Status:** implementado. É a consulta usada pela tela "Meus pagamentos" do web.

**PAG-019: Aluno não vê pagamentos de outro**
*Quando* um aluno chama `GET /Payment/student/{id}`, `GET /Payment/{id}` ou `GET /Payment/enrollment/{id}` para dados de outro aluno
*Então* a resposta é `403`, com `detail` = `"Você não possuí permissão suficiente para acessar este registro!"`
**Status:** implementado

**PAG-020: Admin vê os pagamentos de um aluno removido**
*Dado* um aluno removido que tinha pagamentos
*Quando* um `Admin` chama `GET /Payment/student/{studentId}`, `GET /Payment/{id}` ou `GET /Payment/enrollment/{id}`
*Então* a resposta é `200`, com os pagamentos
**Status:** divergente na rota por aluno: correção proposta para a Fase 3
**Hoje:** por id e por matrícula funciona (as consultas usam `IgnoreQueryFilters()`). Por aluno, o handler procura o aluno com o filtro de removidos ativo e responde `404` `"Estudante não encontrado."`, escondendo o histórico financeiro que a regra 8 garante.

**PAG-021: Pagamento por id**
*Quando* o dono ou um `Admin` chama `GET /Payment/{id}` com um id existente
*Então* a resposta é `200` com o pagamento
*Quando* o id não existe
*Então* a resposta é `404`, com `detail` = `"Pagamento não encontrado."`
**Status:** implementado

**PAG-022: Pagamentos de uma matrícula**
*Quando* o dono da matrícula ou um `Admin` chama `GET /Payment/enrollment/{enrollmentId}`
*Então* a resposta é `200`, com os pagamentos dela, do mais recente para o mais antigo
*E* uma matrícula sem nenhum pagamento devolve `200` com lista vazia
*E* uma matrícula inexistente devolve `404`, com `detail` = `"Matrícula não encontrada."`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** a permissão é verificada pelo aluno do primeiro pagamento. Sem pagamentos, não há de onde tirar o aluno, e a resposta é `404` com `"Matrícula não encontrada ou sem estudante associado."` mesmo para uma matrícula que existe.

**PAG-023: Consultas refletem as escritas imediatamente**
*Dado* um aluno que consultou os próprios pagamentos
*Quando* um `Admin` cria, processa ou estorna um pagamento dele
*Então* a próxima consulta do aluno já mostra o estado novo
**Status:** implementado

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `PAG-004` | Criação concorrente bate no índice único e vira `500` | Traduzir a violação do índice para `409` | Fase 3 |
| `PAG-011` | `Pix` e `Boleto` geram `500` | `422` com `errors.Type` | Fase 3 |
| `PAG-020` | Admin não vê, pela rota por aluno, os pagamentos de aluno removido | Consultar o aluno ignorando o filtro de removidos | Fase 3 |
| `PAG-022` | Matrícula sem pagamentos responde `404` | `200` com lista vazia; `404` só para matrícula inexistente | Fase 3 |
| `PAG-013` | Processamento concorrente cobra duas vezes | Controle de concorrência no pagamento | Com o gateway real |
| `PAG-016` | Motivo do estorno é descartado | Guardar e expor | Com o gateway real |

## Fora de escopo

- Gateway real, Pix e boleto: issue #1.
- Preço cadastrado no curso e cobrança automática na matrícula.
- Pagamento parcelado e estorno parcial.
