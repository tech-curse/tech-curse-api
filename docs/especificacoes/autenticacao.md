# Autenticação e sessão (`AUTH`)

## Objetivo

Permitir que uma pessoa crie a própria conta de aluno, entre no sistema e mantenha a sessão aberta sem digitar a senha de novo, e permitir que um administrador crie contas com outros papéis. A API emite um **access token** JWT de vida curta, enviado em todas as requisições, e um **refresh token** opaco, trocado por um novo par quando o access token vence.

## Regras

1. Há três papéis: `Admin`, `Instructor` e `Student`. Cada usuário tem exatamente um.
2. O registro público cria sempre um `Student`. Só um `Admin` cria usuários com outros papéis.
3. Todo usuário `Student` tem um perfil de estudante, criado junto com o usuário. Se o perfil não puder ser criado, o usuário também não fica criado.
4. O e-mail identifica o usuário no login e é único no sistema.
5. Política de senha: no mínimo 8 caracteres, com pelo menos uma letra minúscula, uma maiúscula, um dígito e um caractere que não seja letra nem dígito.
6. O access token vale 2 horas a partir da emissão. Não há tolerância de relógio na validação.
7. O refresh token vale `Jwt:RefreshTokenDays` dias (padrão `7`). Cada uso gera um novo par e invalida o refresh token anterior (rotação).
8. Cada usuário tem um único refresh token válido por vez: um novo login invalida o anterior.
9. O banco guarda apenas o hash SHA-256 do refresh token. O valor em texto puro só existe na resposta HTTP.
10. Falhas de autenticação não revelam se um e-mail está cadastrado.
11. Os endpoints de `/Auth` têm um limite de requisições próprio, mais restrito que o global (ver `AUTH-037`).

## Contrato HTTP

Todas as rotas ficam sob `/tech-curse/Auth`. Corpo e resposta em JSON, com nomes em *camelCase*.

| Método e rota | Acesso | Corpo | Sucesso |
| --- | --- | --- | --- |
| `POST /register` | público | `{ name, email, password, confirmPassword }` | `201` `{ "mensagem": "Usuário registrado com sucesso." }` |
| `POST /users` | `Admin` | `{ name, email, role, password, confirmPassword }` | `201` `{ "mensagem": "Usuário criado com sucesso." }` |
| `POST /login` | público | `{ email, password }` | `200` `{ accessToken, refreshToken, expiresAt }` |
| `POST /refresh` | público | `{ accessToken, refreshToken }` | `200` `{ accessToken, refreshToken, expiresAt }` |

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
*Então* a resposta é `422`, e `errors` traz um código para cada exigência descumprida: `PasswordTooShort`, `PasswordRequiresLower`, `PasswordRequiresUpper`, `PasswordRequiresDigit` ou `PasswordRequiresNonAlphanumeric`
**Status:** implementado

**AUTH-005: E-mail de usuário existente não pode ser usado de novo**
*Dado* um usuário já cadastrado com um e-mail
*Quando* alguém tenta registrar o mesmo e-mail
*Então* a resposta é `422`, com o código `DuplicateEmail` em `errors`
**Status:** implementado

**AUTH-006: Perfil de estudante antigo sem usuário bloqueia o e-mail**
**Status:** removido. A FK `Student.IdentityUserId` é obrigatória e apaga o perfil em cascata junto com o usuário, então um perfil sem usuário não pode existir no schema atual. O cenário só fazia sentido para dados do projeto anterior, que não existem neste repositório; o código que o tratava saiu.

**AUTH-007: E-mail em formato inválido é recusado**
*Quando* o e-mail não tem formato de e-mail
*Então* a resposta é `422`, com o código `InvalidEmail` em `errors`
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

**AUTH-014: Credenciais válidas devolvem um par de tokens**
*Dado* um usuário cadastrado
*Quando* ele envia `POST /login` com e-mail e senha corretos
*Então* a resposta é `200` com `accessToken`, `refreshToken` e `expiresAt`
*E* `expiresAt` é 2 horas após o instante da emissão
**Status:** implementado

**AUTH-015: O access token carrega identidade e papel**
*Dado* um access token emitido no login
*Então* ele é um JWT HS256 com `iss` = `Jwt:Issuer`, `aud` = `Jwt:Audience`, `nameid` = id do usuário, `email` = e-mail do usuário e `role` = papel do usuário
**Status:** implementado

**AUTH-016: Credenciais erradas não revelam se o e-mail existe**
*Quando* o login usa um e-mail não cadastrado
*Ou* um e-mail cadastrado com a senha errada
*Então* as duas respostas são iguais: `401`, com `detail` = `"E-mail ou senha incorretos."`
**Status:** divergente: correção proposta para a Fase 3
**Hoje:** e-mail inexistente responde `"E-mail ou senha incorretos."`, mas senha errada responde `"Usuário não autenticado."`. A diferença permite descobrir quais e-mails estão cadastrados, e o web mostra a segunda mensagem ao usuário. O Swagger também documenta `400` para credenciais inválidas, mas o código devolve `401`.

**AUTH-017: Login não depende de confirmação de e-mail**
*Dado* um usuário recém-registrado
*Quando* ele faz login
*Então* o login funciona sem nenhuma confirmação de e-mail
**Status:** implementado

**AUTH-018: Tentativas erradas seguidas bloqueiam a conta temporariamente**
*Dado* um usuário que errou a senha 5 vezes seguidas
*Quando* ele tenta de novo, mesmo com a senha certa, antes de 15 minutos
*Então* o login é recusado
**Status:** divergente: planejado para a Fase 5 (endurecimento do login)
**Hoje:** o Identity está configurado com 5 tentativas e 15 minutos, mas o login chama `CheckPasswordSignInAsync` com `lockoutOnFailure: false`, então a conta nunca é bloqueada. O rate limiting (`AUTH-037`) é a única barreira contra força bruta.

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

**AUTH-023: Refresh válido devolve um novo par e invalida o anterior**
*Dado* o par de tokens de um login, com o access token já vencido ou não
*Quando* o cliente envia `POST /refresh` com esse par
*Então* a resposta é `200` com um novo `accessToken`, um novo `refreshToken` e um novo `expiresAt`
*E* o novo access token funciona nos endpoints protegidos
**Status:** implementado

**AUTH-024: Refresh token usado uma vez não vale de novo**
*Dado* um refresh token que já foi trocado por um novo par
*Quando* o cliente tenta usá-lo outra vez
*Então* a resposta é `401`
**Status:** implementado

**AUTH-025: Refresh token vencido encerra a sessão**
*Dado* um refresh token emitido há mais de `Jwt:RefreshTokenDays` dias
*Quando* o cliente tenta usá-lo
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
*E* o refresh token do usuário é apagado
**Status:** implementado

**AUTH-026: Refresh token errado é recusado**
*Quando* o refresh token enviado não corresponde ao do usuário
*Então* a resposta é `401`
**Status:** implementado

**AUTH-027: Access token adulterado no refresh é recusado com 401**
*Quando* o `accessToken` enviado ao `POST /refresh` está malformado, tem assinatura inválida ou usa outro algoritmo
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
**Status:** implementado

**AUTH-028: Usuário removido não renova a sessão**
*Dado* um par de tokens de um usuário que não existe mais
*Quando* o cliente tenta o refresh
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
**Status:** implementado

**AUTH-029: Um novo login invalida a sessão anterior**
*Dado* um usuário logado num dispositivo
*Quando* ele faz login em outro dispositivo
*Então* o refresh token do primeiro dispositivo deixa de valer (`401`)
**Status:** implementado (a Fase 5 deve permitir várias sessões por usuário; ver `AUTH-031`)

**AUTH-030: O refresh token não é guardado em texto puro**
*Dado* um login bem-sucedido
*Então* o valor guardado no banco para o refresh token é o hash SHA-256 (em Base64) do token devolvido, e não o token em si
**Status:** implementado

**AUTH-039: Conta bloqueada não renova a sessão**
*Dado* um usuário com a conta bloqueada, por exemplo um aluno removido por um `Admin` (`ALU-014`)
*Quando* o cliente dele envia `POST /refresh` com um par de tokens que era válido
*Então* a resposta é `401`, com `detail` = `"Refresh Token inválido ou expirado."`
*E* o refresh token dele é apagado
**Status:** implementado

### Sessão: evoluções planejadas

**AUTH-031: Várias sessões por usuário, revogáveis uma a uma**
*Dado* um usuário logado no celular e no computador
*Então* as duas sessões funcionam ao mesmo tempo, e encerrar uma não afeta a outra
**Status:** planejado (Fase 5)

**AUTH-032: Logout revoga a sessão no servidor**
*Quando* o usuário sai
*Então* o refresh token daquela sessão deixa de valer imediatamente
**Status:** planejado (Fase 5). Hoje o "sair" do web apaga só os tokens do navegador; o refresh token continua válido até vencer.

**AUTH-033: Reuso de refresh token revoga a sessão inteira**
*Dado* um refresh token já trocado por um novo par
*Quando* alguém o reapresenta, o que indica roubo
*Então* a sessão inteira daquele token é revogada, inclusive o par mais novo
**Status:** planejado (Fase 5)

**AUTH-034: Refresh token entregue em cookie HttpOnly e access token mais curto**
*Então* o refresh token trafega só num cookie `HttpOnly`, `Secure` e `SameSite`, fora do alcance do JavaScript, e o access token passa a valer minutos, não horas
**Status:** planejado (Fase 5)

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

| Cenário | Hoje | Proposta | Quando |
| --- | --- | --- | --- |
| `AUTH-016` | Mensagens diferentes para e-mail inexistente e senha errada; Swagger documenta `400` | A mesma resposta `401` para os dois casos; Swagger corrigido | Fase 3 |
| `AUTH-018` | Lockout configurado, mas nunca aplicado | Aplicar | Fase 5 |

## Fora de escopo

- Confirmação de e-mail e recuperação de senha: exigem envio de e-mail, que o projeto não tem.
- Login com provedores externos (Google, Microsoft): a decisão é autenticação própria.
- Autenticação em dois fatores.
