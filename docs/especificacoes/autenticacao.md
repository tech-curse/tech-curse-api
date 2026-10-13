# Autenticação e sessão (`AUTH`)

## Objetivo

Permitir que uma pessoa crie a própria conta de aluno, entre no sistema e mantenha a sessão aberta sem digitar a senha de novo, e permitir que um administrador crie contas com outros papéis. A API emite um **access token** JWT de vida curta, enviado em todas as requisições, e um **refresh token** opaco, guardado num cookie `HttpOnly` e trocado por um novo access token quando ele vence.

## Regras

1. Há três papéis: `Admin`, `Instructor` e `Student`. Cada usuário tem exatamente um.
2. O registro público cria sempre um `Student`. Só um `Admin` cria usuários com outros papéis.
3. Todo usuário `Student` tem um perfil de estudante, criado junto com o usuário. Se o perfil não puder ser criado, o usuário também não fica criado.
4. O e-mail identifica o usuário no login e é único no sistema.
5. Política de senha: de 12 a 128 caracteres, com pelo menos uma letra minúscula, uma maiúscula, um dígito e um caractere que não seja letra nem dígito. A política vale ao definir a senha: quem já tinha uma senha mais curta continua entrando com ela. As mensagens de erro do Identity são em português (`DescritorDeErrosDoIdentity`).
6. O access token vale `Jwt:AccessTokenMinutes` minutos (padrão `15`) a partir da emissão. Não há tolerância de relógio na validação.
7. O refresh token vale `Jwt:RefreshTokenDays` dias (padrão `7`) e trafega **só** no cookie `__Secure-tech-curse-sessao`, com `HttpOnly`, `Secure`, `SameSite=Strict` e `Path=/tech-curse/Auth`: o JavaScript não o lê, e o navegador só o envia às rotas de autenticação. Cada uso gera um novo refresh token, num novo cookie, e invalida o anterior (rotação).
8. Cada login abre uma **sessão**. Um usuário pode ter várias sessões ao mesmo tempo (celular e computador), e cada uma é encerrada sem afetar as outras. Um login apaga as sessões já revogadas ou vencidas do próprio usuário.
9. O banco guarda apenas o hash SHA-256 do refresh token (tabela `RefreshTokens`, ligada à sessão em `SessoesDeAutenticacao`). O valor em texto puro só existe na resposta HTTP.
10. Falhas de autenticação não revelam se um e-mail está cadastrado.
11. Os endpoints de `/Auth` têm um limite de requisições próprio, mais restrito que o global (ver `AUTH-037`).

## Contrato HTTP

Todas as rotas ficam sob `/tech-curse/Auth`. Corpo e resposta em JSON, com nomes em *camelCase*.

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `POST /register` | público | `{ name, email, password, confirmPassword }` | `201` `{ "mensagem": "Usuário registrado com sucesso." }` |
| `POST /users` | `Admin` | `{ name, email, role, password, confirmPassword }` | `201` `{ "mensagem": "Usuário criado com sucesso." }` |
| `POST /login` | público | `{ email, password }` | `200` `{ accessToken, expiresAt }` e o cookie da sessão |
| `POST /refresh` | público (cookie da sessão) | sem corpo | `200` `{ accessToken, expiresAt }` e um novo cookie; `401` apaga o cookie |
| `POST /logout` | público (cookie da sessão) | sem corpo | `204`, sempre, e apaga o cookie |

- `role` é texto: `"Admin"`, `"Instructor"` ou `"Student"`.
- `expiresAt` é o instante de expiração do **access token**, em UTC (ISO-8601).
- O access token é um JWT HS256 com `iss` = `Jwt:Issuer`, `aud` = `Jwt:Audience` e as claims `nameid` (id do usuário), `email` e `role`.
- Endpoints protegidos recebem o access token no cabeçalho `Authorization: Bearer <token>`.

## Cenários

### Registro público

**AUTH-001: Registro válido cria um aluno pronto para usar o sistema**
*Dado* um e-mail ainda não cadastrado
*Quando* alguém envia `POST /register` com nome, e-mail e uma senha que atende à política
*Então* a resposta é `201` com `{ "mensagem": "Usuário registrado com sucesso." }`
*E* a pessoa consegue fazer login com esse e-mail e essa senha
*E* o token emitido tem a role `Student`
*E* `GET /tech-curse/Student/me` devolve o perfil de estudante dela
**Status:** implementado

**AUTH-002: O registro público nunca cria outro papel além de Student**
*Quando* o corpo do registro inclui `"role": "Admin"`
*Então* o usuário é criado como `Student` e o campo é ignorado
**Status:** implementado

**AUTH-003: Senha e confirmação precisam ser iguais**
*Quando* `password` e `confirmPassword` são diferentes
*Então* a resposta é `422`, com `errors.Password` = `["A senha e a confirmação de senha não coincidem."]`
*E* nenhum usuário é criado
**Status:** implementado

**AUTH-004: Senha fora da política é recusada com o motivo**
*Quando* a senha não atende à política (regra 5)
*Então* a resposta é `422`, e `errors` traz um código para cada exigência descumprida, com a mensagem:
- `PasswordTooShort`: `"A senha deve ter no mínimo 12 caracteres."`
- `PasswordTooLong`: `"A senha deve ter no máximo 128 caracteres."`
- `PasswordRequiresLower`: `"A senha deve ter pelo menos uma letra minúscula."`
- `PasswordRequiresUpper`: `"A senha deve ter pelo menos uma letra maiúscula."`
- `PasswordRequiresDigit`: `"A senha deve ter pelo menos um número."`
- `PasswordRequiresNonAlphanumeric`: `"A senha deve ter pelo menos um símbolo (caractere que não é letra nem número)."`
**Status:** implementado

**AUTH-005: E-mail de usuário existente não pode ser usado de novo**
*Dado* um usuário já cadastrado com um e-mail
*Quando* alguém tenta registrar o mesmo e-mail
*Então* a resposta é `422`, com `errors.DuplicateEmail` = `["Este e-mail já está em uso."]`
**Status:** implementado

**AUTH-006: Perfil de estudante antigo sem usuário bloqueia o e-mail**
**Status:** removido. A FK `Student.IdentityUserId` é obrigatória e apaga o perfil em cascata junto com o usuário, então um perfil sem usuário não pode existir no schema atual. O cenário só fazia sentido para dados do projeto anterior, que não existem neste repositório; o código que o tratava saiu.

**AUTH-007: E-mail em formato inválido é recusado**
*Quando* o e-mail não tem formato de e-mail
*Então* a resposta é `422`, com `errors.InvalidEmail` = `["Informe um e-mail válido."]`
**Status:** implementado

**AUTH-008: Corpo incompleto ou malformado é rejeitado antes da regra de negócio**
*Quando* falta um campo obrigatório ou o JSON é inválido
*Então* a resposta é `400`, no formato de validação do ASP.NET Core
**Status:** implementado

**AUTH-009: O nome é livre e não identifica a conta**
*Quando* alguém se registra com um nome que tem espaço ou acento (ex.: `"João da Silva"`), ou com o mesmo nome de outro usuário
*Então* o registro é aceito
*Quando* o nome está vazio ou passa de 100 caracteres
*Então* a resposta é `422`, com as mesmas mensagens da edição de perfil (`ALU-012`)
**Status:** implementado na API: o `UserName` do Identity é o e-mail, e o nome fica só no perfil de estudante. O formulário do web acompanha em `WEB-AUTH-007`.

**AUTH-010: Falha ao criar o perfil desfaz a criação do usuário**
*Dado* que a gravação do perfil de estudante falha
*Quando* o registro é processado
*Então* o usuário recém-criado é removido e o erro é devolvido
**Status:** implementado

### Criação de usuário por Admin

**AUTH-011: Admin cria usuário com qualquer papel**
*Dado* um usuário `Admin` autenticado
*Quando* ele envia `POST /users` com `role` = `Admin`, `Instructor` ou `Student`
*Então* a resposta é `201`, e o novo usuário entra com o papel informado
*E* um `Student` criado assim também ganha perfil de estudante
**Status:** implementado

**AUTH-012: Só Admin cria usuários**
*Quando* `POST /users` chega sem token
*Então* a resposta é `401`
*Quando* chega com token de `Student` ou `Instructor`
*Então* a resposta é `403`
**Status:** implementado

**AUTH-013: Papel desconhecido é recusado**
*Quando* `role` não é `Admin`, `Instructor` nem `Student`
*Então* a requisição é recusada (`400` para texto desconhecido; `422` com `errors.Role` para número fora da faixa)
*E* nenhum usuário é criado
**Status:** implementado

### Login

**AUTH-014: Credenciais válidas abrem a sessão**
*Dado* um usuário cadastrado
*Quando* ele envia `POST /login` com e-mail e senha corretos
*Então* a resposta é `200` com `accessToken` e `expiresAt`, e o refresh token vem só no cookie da sessão
*E* `expiresAt` é 15 minutos após o instante da emissão
**Status:** implementado

**AUTH-015: O access token carrega identidade e papel**
*Dado* um access token emitido no login
*Então* ele é um JWT HS256 com `iss` = `Jwt:Issuer`, `aud` = `Jwt:Audience`, `nameid` = id do usuário, `email` = e-mail do usuário e `role` = papel do usuário
**Status:** implementado

**AUTH-016: Credenciais erradas não revelam se o e-mail existe**
*Quando* o login usa um e-mail não cadastrado
*Ou* um e-mail cadastrado com a senha errada
*Então* as duas respostas são iguais: `401`, com `detail` = `"E-mail ou senha incorretos."`
**Status:** implementado

**AUTH-017: Login não depende de confirmação de e-mail**
*Dado* um usuário recém-registrado
*Quando* ele faz login
*Então* o login funciona sem nenhuma confirmação de e-mail
**Status:** implementado

**AUTH-018: Tentativas erradas seguidas bloqueiam a conta temporariamente**
*Dado* um usuário que errou a senha 5 vezes seguidas
*Quando* ele tenta de novo, mesmo com a senha certa, antes de 15 minutos
*Então* o login é recusado com a mesma resposta do `AUTH-016` (`401`, `"E-mail ou senha incorretos."`), para não revelar que a conta existe
*E* passados os 15 minutos, a senha certa volta a funcionar
*E* um login certo zera a contagem: só erros **seguidos** bloqueiam
**Status:** implementado. Quem conhece o e-mail de alguém consegue bloquear a conta dela por 15 minutos; o limite por cliente (`AUTH-037`) reduz o ritmo, e a troca é aceita porque protege a senha contra tentativa e erro.

### Uso do access token

**AUTH-019: Endpoint protegido exige token**
*Quando* um endpoint protegido recebe uma requisição sem token, ou com um token que não é um JWT válido
*Então* a resposta é `401`, com `detail` = `"Acesso negado. Token ausente ou inválido."`
**Status:** implementado

**AUTH-020: Token vencido é recusado sem tolerância**
*Dado* um access token cujo `exp` já passou, ainda que por poucos segundos
*Quando* ele é usado num endpoint protegido
*Então* a resposta é `401`
**Status:** implementado

**AUTH-021: Papel insuficiente é recusado**
*Dado* um access token válido de um papel sem acesso ao endpoint
*Quando* ele é usado nesse endpoint
*Então* a resposta é `403`, com `detail` = `"Você não tem permissão para acessar este recurso."`
**Status:** implementado

**AUTH-022: Token de outro emissor não vale**
*Quando* o access token foi assinado com outra chave, ou tem `iss` ou `aud` diferentes da configuração
*Então* a resposta é `401`
**Status:** implementado

### Renovação da sessão (refresh)

**AUTH-023: Refresh válido devolve um novo access token e troca o cookie**
*Dado* o cookie da sessão de um login, com o access token já vencido ou não
*Quando* o cliente envia `POST /refresh` com esse cookie
*Então* a resposta é `200` com um novo `accessToken` e um novo `expiresAt`, e o cookie passa a ter um novo refresh token
*E* o novo access token funciona nos endpoints protegidos
**Status:** implementado

**AUTH-024: Refresh token usado uma vez não vale de novo**
*Dado* um refresh token que já foi trocado por um novo
*Quando* o cliente tenta usá-lo outra vez
*Então* a resposta é `401`
**Status:** implementado

**AUTH-025: Refresh token vencido encerra a sessão**
*Dado* um refresh token emitido há mais de `Jwt:RefreshTokenDays` dias
*Quando* o cliente tenta usá-lo
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
*E* a sessão daquele token é revogada
**Status:** implementado

**AUTH-026: Refresh token errado é recusado**
*Quando* o refresh token enviado não corresponde ao do usuário
*Então* a resposta é `401`
**Status:** implementado

**AUTH-027: Access token adulterado no refresh é recusado com 401**
*Quando* o `accessToken` enviado ao `POST /refresh` está malformado, tem assinatura inválida ou usa outro algoritmo
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
**Status:** removido na Fase 5. O refresh não recebe mais o access token: a sessão vem só do cookie (`AUTH-034`).

**AUTH-028: Usuário removido não renova a sessão**
*Dado* o cookie da sessão de um usuário que não existe mais
*Quando* o cliente tenta o refresh
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
**Status:** implementado

**AUTH-029: Um novo login invalida a sessão anterior**
*Dado* um usuário logado num dispositivo
*Quando* ele faz login em outro dispositivo
*Então* o refresh token do primeiro dispositivo deixa de valer (`401`)
**Status:** removido na Fase 5. Um usuário passa a ter várias sessões ao mesmo tempo (`AUTH-031`).

**AUTH-030: O refresh token não é guardado em texto puro**
*Dado* um login bem-sucedido
*Então* o valor guardado no banco (`RefreshTokens.Hash`) é o hash SHA-256 (em Base64) do token devolvido, e não o token em si
**Status:** implementado

**AUTH-039: Conta bloqueada não renova a sessão**
*Dado* um usuário com a conta bloqueada, por exemplo um aluno removido por um `Admin` (`ALU-014`)
*Quando* o cliente dele envia `POST /refresh` com um par de tokens que era válido
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
*E* todas as sessões dele são revogadas
**Status:** implementado

### Sessões

**AUTH-031: Várias sessões por usuário, revogáveis uma a uma**
*Dado* um usuário logado no celular e no computador
*Então* as duas sessões funcionam ao mesmo tempo, e encerrar uma não afeta a outra
**Status:** implementado

**AUTH-032: Logout revoga a sessão no servidor**
*Quando* o usuário sai
*Então* o cliente envia `POST /logout` com o refresh token, e a sessão daquele token deixa de valer imediatamente
*E* a resposta é `204` mesmo para um token desconhecido ou já encerrado, para não revelar se ele existe
**Status:** implementado na API. O "sair" do web passa a chamar o logout em `WEB-AUTH-020`.

**AUTH-033: Reuso de refresh token revoga a sessão inteira**
*Dado* um refresh token já trocado por um novo par
*Quando* alguém o reapresenta, o que indica roubo
*Então* a resposta é `401`, e a sessão inteira daquele token é revogada, inclusive o par mais novo
*E* duas renovações simultâneas com o mesmo token não geram duas sessões válidas: marcar o token como usado é condicional (`UPDATE ... WHERE UsadoEm IS NULL`), e quem chega depois é tratado como reuso
**Status:** implementado

**AUTH-034: Refresh token entregue em cookie HttpOnly e access token curto**
*Quando* o login, o refresh ou o logout respondem
*Então* o refresh token nunca aparece no corpo: só no cookie `__Secure-tech-curse-sessao`, com `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/tech-curse/Auth` e expiração igual à do refresh token
*E* o refresh e o logout leem o cookie, sem corpo; refresh sem cookie, ou recusado, responde `401` e apaga o cookie; o logout sempre o apaga
*E* o access token vale `Jwt:AccessTokenMinutes` minutos (padrão `15`)
**Status:** implementado. O web passa a usar o cookie em `WEB-AUTH-022`.

### Configuração

**AUTH-035: A API não sobe sem uma chave de assinatura forte**
*Quando* `Jwt:SigningKey` está ausente ou tem menos de 32 caracteres
*Então* a aplicação falha no startup, com uma mensagem que cita `SigningKey`
**Status:** implementado

**AUTH-036: Qualquer chave de assinatura válida funciona**
*Dado* um `Jwt:SigningKey` com caracteres fora do ASCII
*Quando* um usuário faz login e usa o token
*Então* o token é aceito
**Status:** implementado

**AUTH-037: Endpoints de autenticação têm limite de requisições próprio**
*Dado* um mesmo cliente
*Quando* ele faz mais de `RateLimiting:AuthPermitLimit` (padrão `10`) requisições a `/Auth` dentro de `RateLimiting:AuthWindowSeconds` (padrão `60`) segundos
*Então* as excedentes recebem `429`, com o cabeçalho `Retry-After`
**Status:** implementado (a identificação do cliente atrás de proxy reverso está em `transversais.md`)

**AUTH-038: Admin de desenvolvimento semeado só em Development**
*Dado* `Seed:Admin:Email` e `Seed:Admin:Password` configurados
*Quando* a API sobe em `Development`
*Então* existe um usuário `Admin` com esse e-mail (criado uma vez só)
*Quando* a API sobe em qualquer outro ambiente
*Então* nenhum Admin é semeado
**Status:** implementado

## Divergências

Nenhuma: o comportamento atual segue esta especificação.

## Fora de escopo

- Confirmação de e-mail e recuperação de senha: exigem envio de e-mail, que o projeto não tem.
- Login com provedores externos (Google, Microsoft): a decisão é autenticação própria.
- Autenticação em dois fatores.
