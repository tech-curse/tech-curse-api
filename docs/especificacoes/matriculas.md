# Matrículas (`MAT`)

## Objetivo

Registrar que um aluno está inscrito num curso. A matrícula é o vínculo que libera o curso na área do aluno e a base de qualquer pagamento.

## Regras

1. Um aluno tem no máximo uma matrícula por curso.
2. O aluno se matricula sozinho. O `Admin` matricula qualquer aluno ativo.
3. A matrícula é criada ativa, com a data em UTC, e independe de pagamento: o pagamento é uma operação separada (ver `pagamentos.md`).
4. Aluno removido não ganha matrícula nova.

## Contrato HTTP

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `POST /tech-curse/Enrollment` | `Student`, `Admin` | `{ courseId, studentId }` | `201` (ver `MAT-001`) |

- Para um `Student`, o `studentId` do corpo é ignorado: a matrícula é sempre dele mesmo (identificado pelo token). O campo continua obrigatório e maior que zero; o web envia o id do próprio perfil.
- Para um `Admin`, o `studentId` diz quem será matriculado.
- As matrículas de um aluno são consultadas em `GET /tech-curse/Student/{id}/enrollments` (`ALU-019`).

## Cenários

**MAT-001: Aluno se matricula num curso**
*Dado* um aluno ativo e um curso em que ele não está matriculado
*Quando* ele envia `POST /Enrollment` com o `courseId` do curso
*Então* a resposta é `201`, com `{ "mensagem": "Aluno matriculado com sucesso." }`
*E* o curso aparece em `GET /Student/{id}/enrollments` dele, com `matriculaAtiva` = `true`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** a resposta é `202 Accepted`, que significa "aceito para processar depois". A matrícula, porém, já está gravada quando a resposta sai. O web não lê o status nem o corpo, então a troca não o afeta.

**MAT-002: Aluno só matricula a si mesmo**
*Dado* um aluno que envia o `studentId` de outro aluno
*Quando* ele chama `POST /Enrollment`
*Então* a matrícula é criada para ele mesmo, e o outro aluno não ganha matrícula
**Status:** implementado

**MAT-003: Admin matricula um aluno**
*Dado* um aluno ativo
*Quando* um `Admin` envia `POST /Enrollment` com o `studentId` e o `courseId`
*Então* o aluno fica matriculado no curso
**Status:** implementado (com o status `201` do `MAT-001`)

**MAT-004: Só aluno e Admin criam matrículas**
*Quando* um `Instructor` chama `POST /Enrollment`
*Então* a resposta é `403`, com `detail` = `"Apenas estudantes e administradores podem criar matrículas!"`
*Quando* a chamada vem sem token
*Então* a resposta é `401`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** o `Instructor` recebe `409 Conflict` (`NotAllowedException`), como em `ALU-005`.

**MAT-005: Matrícula duplicada é recusada**
*Dado* um aluno já matriculado num curso, com a matrícula ativa ou não
*Quando* ele tenta se matricular de novo no mesmo curso
*Então* a resposta é `409`, com `detail` = `"Estudante já está matriculado neste curso!"`
*E* continua existindo uma matrícula só
**Status:** implementado

**MAT-006: Duas requisições simultâneas não criam duas matrículas**
*Dado* um aluno sem matrícula num curso
*Quando* duas requisições de matrícula no mesmo curso chegam ao mesmo tempo (um clique duplo, por exemplo)
*Então* uma resposta é `201` e a outra `409`, e existe uma matrícula só
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** a unicidade é checada só no código, sem índice único em (aluno, curso) no banco. As duas requisições passam pela checagem antes de qualquer uma gravar, e o aluno fica com duas matrículas no mesmo curso.

**MAT-007: Curso inexistente**
*Quando* o `courseId` não existe
*Então* a resposta é `404`, com `detail` = `"Curso não encontrado!"`
**Status:** implementado

**MAT-008: Aluno inexistente ou removido**
*Quando* um `Admin` envia o `studentId` de um aluno que não existe ou foi removido
*Então* a resposta é `404`, com `detail` = `"Estudante não encontrado!"`
*Quando* um usuário `Student` sem perfil ativo tenta se matricular
*Então* a resposta é a mesma
**Status:** implementado

**MAT-009: Ids inválidos são recusados**
*Quando* `courseId` ou `studentId` é zero ou negativo
*Então* a resposta é `422`, com `errors.CourseId` = `["O ID do curso deve ser maior que zero."]` ou `errors.StudentId` = `["O ID do estudante deve ser maior que zero."]`
**Status:** implementado

**MAT-010: Curso com matrícula não pode ser removido**
*Dado* um curso com matrícula
*Então* a remoção do curso é recusada (`CUR-015`)
**Status:** implementado

## Divergências

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `MAT-001` | `202 Accepted` para uma operação já concluída | `201 Created` | Fase 3 |
| `MAT-004` | `Instructor` recebe `409` | `403` | Fase 3 |
| `MAT-006` | Clique duplo cria duas matrículas no mesmo curso | Índice único em (aluno, curso) no banco, com a violação traduzida para `409` | Fase 3 |

## Fora de escopo

- Cancelar ou trancar matrícula: não há operação que torne uma matrícula inativa. O campo `matriculaAtiva` existe, mas hoje é sempre `true`.
- Exigir pagamento para liberar o curso.
- Limite de vagas por curso.
