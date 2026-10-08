using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Api.Configuration;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

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
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito. Já existe um perfil de estudante com este e-mail.", typeof(ProblemDetails))]
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
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito. Já existe um perfil de estudante com este e-mail.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação nos campos enviados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserInputDto input)
    {
        await _authService.CreateUserAsync(input);

        return StatusCode(201, new MensagemOutputDto("Usuário criado com sucesso."));
    }

    [HttpPost("login")]
    [SwaggerOperation(
        Summary = "Realiza o login de um usuário e retorna o Token JWT.",
        Description = "**Acesso:** Público."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Autenticação bem-sucedida. Retorna o Token JWT.", typeof(AuthOutputDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Credenciais inválidas.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado ou inativo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de tentativas de login excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Login([FromBody] LoginInputDto input)
    {
        var authResult = await _authService.LoginAsync(input);

        return Ok(authResult);
    }

    [HttpPost("refresh")]
    [SwaggerOperation(
        Summary = "Gera um novo Token JWT a partir de um Refresh Token válido.",
        Description = "**Acesso:** Público."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Token atualizado com sucesso.", typeof(AuthOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Refresh token expirado ou inválido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenInputDto input)
    {
        var refreshResult = await _authService.RefreshAsync(input);

        return Ok(refreshResult);
    }
}
