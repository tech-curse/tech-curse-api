# Alunos (`ALU`)

## Objetivo

Manter o perfil de estudante de cada usuário `Student`: os dados que o aluno vê e edita sobre si mesmo, e o que o administrador gerencia.

## Regras

1. O perfil de estudante nasce junto com o usuário `Student` (`AUTH-001`, `AUTH-011`).
2. O perfil tem nome, e-mail e data de cadastro (UTC). O nome tem de 1 a 100 caracteres. O e-mail vem da conta e não é editável pelo perfil.
3. O aluno vê e edita só o próprio perfil. O `Admin` vê, edita e remove qualquer perfil.
4. A remoção é lógica (*soft delete*): o perfil some das consultas, mas matrículas e pagamentos continuam no banco e no histórico financeiro (ver `pagamentos.md`).
5. Remover o aluno encerra o acesso dele: a conta fica bloqueada para login e para renovação de sessão.

## Contrato HTTP

Rotas sob `/tech-curse/Student`.

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `GET /me` | `Student` | | `200` perfil |
| `GET /` | `Admin` | query: paginação (`TRV-027`) | `200` lista paginada de perfis |
| `GET /{id}` | o próprio aluno ou `Admin` | | `200` perfil |
| `GET /{id}/enrollments` | o próprio aluno ou `Admin` | | `200` lista de matrículas |
| `PUT /{id}` | o próprio aluno ou `Admin` | `{ nome }` | `204` |
| `DELETE /{id}` | `Admin` | | `204` |
| `POST /` | `Admin` | `{ nome, email }` | `201` perfil (ver `ALU-020`) |

- Perfil: `{ id, nome, email, dataCadastro }`.
- Matrícula do aluno: `{ courseId, titulo, descricao, categoria, matriculaAtiva, enrollmentId }`.

## Cenários

### Próprio perfil

**ALU-001: Aluno consulta o próprio perfil**
*Dado* um usuário `Student` com perfil
*Quando* ele chama `GET /Student/me`
*Então* a resposta é `200` com o perfil dele
**Status:** implementado

**ALU-002: Só aluno tem `/me`**
*Quando* um `Admin` ou um `Instructor` chama `GET /Student/me`
*Então* a resposta é `403`
**Status:** implementado

**ALU-003: Aluno sem perfil recebe 404**
*Dado* um usuário `Student` sem perfil de estudante
*Quando* ele chama `GET /Student/me`
*Então* a resposta é `404`, com `detail` = `"Perfil de estudante não encontrado ou inativo."`
**Status:** implementado. O web trata esse `404` como o estado "perfil pendente".

### Consulta por id

**ALU-004: Aluno vê o próprio perfil por id; Admin vê qualquer um**
*Quando* um aluno chama `GET /Student/{id}` com o id do próprio perfil
*Ou* um `Admin` chama com qualquer id existente
*Então* a resposta é `200` com o perfil
**Status:** implementado

**ALU-005: Aluno não vê o perfil de outro aluno**
*Quando* um aluno chama `GET /Student/{id}` com o id de outro aluno
*Então* a resposta é `403`, com `detail` = `"Você não possui permissão suficiente para acessar este registro."`
**Status:** implementado

**ALU-006: Perfil inexistente ou removido**
*Quando* `GET /Student/{id}` usa um id que não existe ou de um aluno removido
*Então* a resposta é `404`, com `detail` = `"Estudante não encontrado."`
**Status:** implementado

### Listagem (Admin)

**ALU-007: Admin lista os alunos, sem os removidos**
*Dado* alunos ativos e alunos removidos
*Quando* um `Admin` chama `GET /Student`
*Então* a resposta é `200`, com os ativos em lista paginada (`TRV-027`), e os removidos não aparecem nem em `totalCount`
**Status:** implementado

**ALU-008: Listagem tem ordem estável**
*Quando* um `Admin` percorre as páginas de `GET /Student`
*Então* os alunos vêm ordenados pelo id (ou por `SortBy` = `nome` ou `datacadastro`, na direção de `SortDirection`), e nenhum aluno se repete nem some entre páginas
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** a consulta não tem nenhuma ordenação; `SortBy` e `SortDirection` são ignorados. Sem `ORDER BY`, o PostgreSQL não garante a ordem, e a mesma pessoa pode aparecer em duas páginas enquanto outra não aparece em nenhuma.

**ALU-009: Só Admin lista alunos**
*Quando* um `Student` ou um `Instructor` chama `GET /Student`
*Então* a resposta é `403`
**Status:** implementado

### Edição

**ALU-010: Aluno edita o próprio nome; Admin edita qualquer um**
*Quando* o aluno envia `PUT /Student/{id}` para o próprio perfil, ou um `Admin` para qualquer perfil, com `{ "nome": "Novo Nome" }`
*Então* a resposta é `204`, e o perfil passa a ter o nome novo
*E* o e-mail e a data de cadastro não mudam
**Status:** implementado

**ALU-011: Aluno não edita o perfil de outro**
*Quando* um aluno envia `PUT /Student/{id}` com o id de outro aluno
*Então* a resposta é `403`, com `detail` = `"Você não possui permissão suficiente para atualizar este registro."`, e nada muda
**Status:** implementado

**ALU-012: Nome inválido é recusado**
*Quando* o nome está vazio ou passa de 100 caracteres
*Então* a resposta é `422`, com `errors.Nome` = `["O nome é obrigatório."]` ou `["O nome deve ter no máximo 100 caracteres."]`
**Status:** implementado

**ALU-013: Edição de perfil inexistente ou removido**
*Quando* `PUT /Student/{id}` usa um id que não existe ou de um aluno removido
*Então* a resposta é `404`, com `detail` = `"Estudante não encontrado."`
**Status:** implementado

### Remoção

**ALU-014: Admin remove um aluno**
*Dado* um aluno ativo
*Quando* um `Admin` envia `DELETE /Student/{id}`
*Então* a resposta é `204`
*E* o perfil some de `GET /Student` e `GET /Student/{id}` (`404`)
*E* o login desse aluno passa a ser recusado com `401`
**Status:** implementado

**ALU-015: Remover de novo ou remover inexistente**
*Quando* `DELETE /Student/{id}` usa um id que não existe ou de um aluno já removido
*Então* a resposta é `404`, com `detail` = `"Estudante não encontrado."`
**Status:** implementado

**ALU-016: Aluno removido não mantém a sessão aberta**
*Dado* um aluno logado que é removido por um `Admin`
*Quando* o cliente dele tenta renovar a sessão com `POST /Auth/refresh`
*Então* a resposta é `401` (ver `AUTH-039`)
**Status:** implementado

**ALU-017: Só Admin remove**
*Quando* um `Student` ou um `Instructor` envia `DELETE /Student/{id}`
*Então* a resposta é `403`
**Status:** implementado

**ALU-018: O histórico do aluno removido continua existindo**
*Dado* um aluno com matrículas e pagamentos
*Quando* ele é removido
*Então* as matrículas e os pagamentos continuam no banco, e os pagamentos continuam aparecendo nas consultas do `Admin` (ver `pagamentos.md`)
**Status:** implementado

### Matrículas do aluno

**ALU-019: Aluno vê as próprias matrículas; Admin vê as de qualquer um**
*Dado* um aluno matriculado em cursos
*Quando* ele chama `GET /Student/{id}/enrollments` com o próprio id, ou um `Admin` chama com qualquer id
*Então* a resposta é `200`, com uma entrada por matrícula: `courseId`, `titulo`, `descricao`, `categoria`, `matriculaAtiva` e `enrollmentId`
*Quando* um aluno chama com o id de outro aluno
*Então* a resposta é `403`
**Status:** implementado

### Criação de perfil por Admin

**ALU-020: `POST /Student` deixa de existir**
*Então* `POST /Student` responde `404`, porque o perfil sempre nasce com o usuário (`AUTH-001`, `AUTH-011`)
**Status:** divergente: proposta de remoção na Fase 3
**Hoje:** o endpoint cria um perfil para um usuário que já existe. Ele servia a contas criadas antes de o registro criar o perfil, situação que não existe neste repositório. E tem problemas próprios: responde `409` com `"Usuário não encontrado."` quando não há usuário com o e-mail, não verifica se o usuário é `Student` (cria perfil de aluno para um `Instructor`), e ignora alunos removidos ao checar e-mail repetido (o mesmo usuário pode ganhar um segundo perfil).

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `ALU-008` | Listagem sem ordenação: alunos repetidos ou omitidos entre páginas | Ordem por id, ou por `nome` / `datacadastro` | Fase 3 |
| `ALU-020` | `POST /Student` sem uso e com falhas próprias | Remover o endpoint | Fase 3 |

## Fora de escopo

- Reativar um aluno removido.
- Alterar o e-mail da conta.
- Remoção definitiva dos dados (anonimização).
