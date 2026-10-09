# Cursos (`CUR`)

## Objetivo

Manter o catálogo de cursos que os alunos consultam e no qual se matriculam.

## Regras

1. Um curso tem título, descrição, categoria e carga horária (em horas). A data de criação é registrada pela API, em UTC.
2. Título: obrigatório, até 100 caracteres. Descrição: obrigatória. Categoria: obrigatória, até 50 caracteres. Carga horária: inteiro maior que zero.
3. Títulos podem se repetir.
4. Qualquer usuário autenticado consulta o catálogo. `Admin` e `Instructor` criam cursos. Só `Admin` edita e remove.
5. Um curso com qualquer matrícula, ativa ou não, não pode ser removido: o histórico do aluno depende dele.

## Contrato HTTP

Rotas sob `/tech-curse/Course`.

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `GET /` | autenticado | query: paginação (`TRV-027`) e `Categoria` | `200` lista paginada de cursos |
| `GET /{id}` | autenticado | | `200` curso |
| `POST /` | `Admin`, `Instructor` | `{ titulo, descricao, categoria, cargaHoraria }` | `201` curso, com `Location` apontando para `GET /{id}` |
| `PUT /{id}` | `Admin` | `{ titulo, descricao, categoria, cargaHoraria }` | `204` |
| `DELETE /{id}` | `Admin` | | `204` |

Curso na resposta: `{ id, titulo, descricao, categoria, cargaHoraria, dataCriacao }`.

Ordenações aceitas em `SortBy` (sem diferença entre maiúsculas e minúsculas): `titulo`, `categoria`, `datacriacao`; qualquer outro valor ordena pelo id (`TRV-032`).

## Cenários

### Consulta

**CUR-001: Catálogo paginado**
*Dado* cursos cadastrados
*Quando* um usuário autenticado chama `GET /Course`
*Então* a resposta é `200`, com os cursos no formato de lista paginada (`TRV-027` a `TRV-031`)
**Status:** implementado

**CUR-002: Catálogo exige autenticação**
*Quando* `GET /Course` ou `GET /Course/{id}` chega sem token
*Então* a resposta é `401`
**Status:** implementado

**CUR-003: Filtro por categoria**
*Dado* cursos das categorias `"Backend"` e `"Frontend"`
*Quando* a listagem é chamada com `Categoria=Backend`
*Então* só os cursos de `"Backend"` aparecem, e `totalCount` conta só eles
*E* a comparação é exata: `Categoria=backend` não encontra `"Backend"`
**Status:** implementado

**CUR-004: Ordenação por título, categoria ou data de criação**
*Quando* a listagem é chamada com `SortBy=titulo&SortDirection=desc`
*Então* os cursos vêm em ordem decrescente de título
*E* o mesmo vale para `categoria` e `datacriacao`, nas duas direções
**Status:** implementado

**CUR-005: Detalhe de um curso**
*Quando* `GET /Course/{id}` é chamado com o id de um curso existente
*Então* a resposta é `200` com o curso
**Status:** implementado

**CUR-006: Curso inexistente**
*Quando* `GET /Course/{id}` é chamado com um id que não existe
*Então* a resposta é `404`, com `detail` = `"Curso não encontrado."`
**Status:** implementado

### Criação

**CUR-007: Admin ou Instructor cria um curso**
*Dado* um usuário `Admin` ou `Instructor`
*Quando* ele envia `POST /Course` com dados válidos
*Então* a resposta é `201`, com o curso criado (incluindo `id` e `dataCriacao`) e o cabeçalho `Location` apontando para `/tech-curse/Course/{id}`
*E* o curso aparece no catálogo para os alunos
**Status:** implementado

**CUR-008: Aluno não cria curso**
*Quando* um `Student` envia `POST /Course`
*Então* a resposta é `403`
**Status:** implementado

**CUR-009: Dados inválidos são recusados com o motivo**
*Quando* o título está vazio ou passa de 100 caracteres, a descrição está vazia, a categoria está vazia ou passa de 50 caracteres, ou a carga horária é zero ou negativa
*Então* a resposta é `422`, e `errors` traz o campo e a mensagem:
- `Titulo`: `"O título é obrigatório."` / `"O título deve ter no máximo 100 caracteres."`
- `Descricao`: `"A descrição é obrigatória."`
- `Categoria`: `"A categoria é obrigatória."` / `"A categoria deve ter no máximo 50 caracteres."`
- `CargaHoraria`: `"A carga horária deve ser maior que zero."`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** na **criação**, a categoria não tem limite de tamanho; só a edição limita a 50 caracteres. Um curso pode ser criado com uma categoria que depois impede a edição.

### Edição

**CUR-010: Admin edita um curso**
*Dado* um curso existente
*Quando* um `Admin` envia `PUT /Course/{id}` com dados válidos
*Então* a resposta é `204`, e `GET /Course/{id}` passa a devolver os dados novos
*E* `dataCriacao` não muda
**Status:** implementado

**CUR-011: Só Admin edita**
*Quando* um `Instructor` ou um `Student` envia `PUT /Course/{id}`
*Então* a resposta é `403`
**Status:** implementado

**CUR-012: Edição valida os mesmos campos da criação**
*Quando* o corpo do `PUT` viola as regras do `CUR-009`
*Então* a resposta é `422`, com as mesmas mensagens
**Status:** implementado

**CUR-013: Edição de curso inexistente**
*Quando* `PUT /Course/{id}` usa um id que não existe
*Então* a resposta é `404`, com `detail` = `"Curso não encontrado."`
**Status:** implementado

### Remoção

**CUR-014: Admin remove um curso sem matrículas**
*Dado* um curso sem nenhuma matrícula
*Quando* um `Admin` envia `DELETE /Course/{id}`
*Então* a resposta é `204`
*E* o curso some do catálogo, e `GET /Course/{id}` passa a responder `404`
**Status:** implementado

**CUR-015: Curso com matrícula não é removido**
*Dado* um curso com pelo menos uma matrícula, ativa ou não
*Quando* um `Admin` tenta removê-lo
*Então* a resposta é `409`, com `detail` = `"O curso possui matrículas ativas."`
*E* o curso continua no catálogo
**Status:** implementado

**CUR-016: Só Admin remove**
*Quando* um `Instructor` ou um `Student` envia `DELETE /Course/{id}`
*Então* a resposta é `403`
**Status:** implementado

**CUR-017: Remoção de curso inexistente**
*Quando* `DELETE /Course/{id}` usa um id que não existe
*Então* a resposta é `404`, com `detail` = `"Curso não encontrado."`
**Status:** implementado

### Consistência do catálogo

**CUR-018: Toda mudança aparece imediatamente para todos**
*Dado* um aluno que já consultou o catálogo e o detalhe de um curso
*Quando* um `Admin` cria, edita ou remove um curso
*Então* a próxima consulta do aluno já reflete a mudança
**Status:** implementado

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `CUR-009` | Criação aceita categoria de qualquer tamanho; edição limita a 50 | Limite de 50 também na criação | Fase 3 |

## Fora de escopo

- Conteúdo do curso (aulas, vídeos, materiais) e progresso do aluno.
- Curso vinculado a um instrutor responsável: o `Instructor` cria, mas não "é dono" do curso.
- Busca por texto livre.
