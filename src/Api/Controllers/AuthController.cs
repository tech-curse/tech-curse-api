using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Api.Configuration;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Auth")]
[EnableRateLimiting(RateLimitingSetup.PoliticaAutenticacao)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authAppService)
    {
        _authService = authAppService;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Registra um novo usuário no sistema.",
        Description = "**Acesso:** Público. O usuário é sempre criado com a role Student."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Usuário registrado com sucesso.", typeof(MensagemOutputDto))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação nos campos enviados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Register([FromBody] RegisterInputDto input)
    {
        await _authService.RegisterAsync(input);

        return StatusCode(201, new MensagemOutputDto("Usuário registrado com sucesso."));
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Cria um usuário com a role informada.",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Usuário criado com sucesso.", typeof(MensagemOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação nos campos enviados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserInputDto input)
    {
        await _authService.CreateUserAsync(input);

        return StatusCode(201, new MensagemOutputDto("Usuário criado com sucesso."));
    }

    [HttpPost("login")]
    [SwaggerOperation(
        Summary = "Realiza o login, devolve o access token e grava o refresh token num cookie HttpOnly.",
        Description = "**Acesso:** Público. O refresh token vai no cookie `__Secure-tech-curse-sessao` (HttpOnly, Secure, SameSite=Strict, Path=/tech-curse/Auth), nunca no corpo."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Autenticação bem-sucedida. Retorna o Token JWT.", typeof(AuthOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "E-mail ou senha incorretos, ou conta bloqueada.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de tentativas de login excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Login([FromBody] LoginInputDto input)
    {
        var sessao = await _authService.LoginAsync(input);
        GravarCookieDaSessao(sessao);

        return Ok(new AuthOutputDto(sessao.AccessToken, sessao.ExpiresAt));
    }

    [HttpPost("refresh")]
    [SwaggerOperation(
        Summary = "Troca o refresh token do cookie por um novo access token e um novo cookie.",
        Description = "**Acesso:** Público. Lê o refresh token do cookie `__Secure-tech-curse-sessao`; sem corpo."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Token atualizado com sucesso.", typeof(AuthOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Refresh token expirado ou inválido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            var sessao = await _authService.RefreshAsync(Request.Cookies[CookieDaSessao.Nome]);
            GravarCookieDaSessao(sessao);

            return Ok(new AuthOutputDto(sessao.AccessToken, sessao.ExpiresAt));
        }
        catch (UnauthorizedException)
        {
            ApagarCookieDaSessao();
            throw;
        }
    }

    [HttpPost("logout")]
    [SwaggerOperation(
        Summary = "Encerra a sessão do refresh token do cookie e apaga o cookie.",
        Description = "**Acesso:** Público. Responde 204 mesmo sem cookie ou com um token desconhecido, para não revelar se ele existe."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Sessão encerrada.")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(Request.Cookies[CookieDaSessao.Nome]);
        ApagarCookieDaSessao();

        return NoContent();
    }

    private void GravarCookieDaSessao(SessaoEmitidaDto sessao) =>
        Response.Cookies.Append(CookieDaSessao.Nome, sessao.RefreshToken, CookieDaSessao.Opcoes(sessao.RefreshTokenExpiraEm));

    private void ApagarCookieDaSessao() =>
        Response.Cookies.Delete(CookieDaSessao.Nome, CookieDaSessao.Opcoes(null));
}
