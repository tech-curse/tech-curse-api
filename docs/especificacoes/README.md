# Especificação técnica da Tech Curse API

Esta pasta descreve **como a API deve se comportar**. É a fonte da verdade para o comportamento: os testes automatizados derivam daqui, e uma mudança de comportamento altera a especificação, o código e os testes no mesmo pull request.

Ela não explica *como* o código é organizado nem *por que* uma técnica foi escolhida. Isso fica no [`CLAUDE.md`](../../CLAUDE.md). Instruções de uso ficam no [README](../../README.md).

## Áreas

| Área | Arquivo | Prefixo | Situação |
| --- | --- | --- | --- |
| Autenticação e sessão | [autenticacao.md](autenticacao.md) | `AUTH` | escrita |
| Cursos | `cursos.md` | `CUR` | a escrever |
| Alunos | `alunos.md` | `ALU` | a escrever |
| Matrículas | `matriculas.md` | `MAT` | a escrever |
| Pagamentos | `pagamentos.md` | `PAG` | a escrever |
| Comportamentos transversais (erros, rate limiting, CORS, health checks, idempotência) | `transversais.md` | `TRV` | a escrever |

## Como ler um cenário

Cada área tem regras, o contrato HTTP e uma lista de **cenários** no formato *Dado / Quando / Então*. Todo cenário tem um ID (`AUTH-014`) e um status:

| Status | Significado | O que acontece com o teste |
| --- | --- | --- |
| **implementado** | O código faz exatamente o que o cenário descreve | Tem (ou vai ter) teste automatizado agora |
| **divergente** | O código faz outra coisa; o cenário descreve o comportamento **desejado** | O cenário diz quando a correção entra; o teste nasce com a correção |
| **planejado (Fase N)** | O comportamento ainda não existe | O teste nasce junto com a funcionalidade |

Um cenário divergente sempre traz a linha **Hoje:**, com o que o código faz de fato.

## Convenções

- IDs nunca são reaproveitados nem renumerados. Um cenário que deixa de valer é marcado como **removido**, com o motivo, e o ID não volta a ser usado.
- Status HTTP, nomes de campos e mensagens citados entre crases são contrato: o teste confere exatamente esse valor.
- Erros seguem o formato `ProblemDetails` descrito em `transversais.md`.
- Valores configuráveis aparecem com a chave de configuração e o padrão, por exemplo `Jwt:RefreshTokenDays` (padrão `7`).

## Rastreabilidade com os testes

Cada teste declara o cenário que cobre com `[Trait("Especificacao", "AUTH-014")]`. Um passo do CI vai conferir que todo cenário **implementado** tem pelo menos um teste. Um cenário pode ter vários testes, e um teste pode cobrir mais de um cenário.
