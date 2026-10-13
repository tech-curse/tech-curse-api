using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using TechCurse.Infrastructure.Data;
using TechCurse.Infrastructure.Identity.Sessoes;

namespace TechCurse.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private const string MensagemDeRefreshInvalido = "Refresh Token inválido ou expirado.";

    private const string MensagemDeCredenciaisInvalidas = "E-mail ou senha incorretos.";

    private const int TamanhoMaximoDoNome = 100;

    private static readonly HashSet<string> CodigosDeUserName = ["DuplicateUserName", "InvalidUserName"];

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IStudentRepository _studentRepository;
    private readonly TechCurseContext _context;

    public AuthService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ITokenService tokenService, IStudentRepository studentRepository, TechCurseContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _studentRepository = studentRepository;
        _context = context;
    }

    public async Task RegisterAsync(RegisterInputDto input)
    {
        await CriarUsuarioAsync(input.Name, input.Email, input.Password, input.ConfirmPassword, UserRole.Student);
    }

    public async Task CreateUserAsync(CreateUserInputDto input)
    {
        await CriarUsuarioAsync(input.Name, input.Email, input.Password, input.ConfirmPassword, input.Role);
    }

    private async Task<IdentityUser> CriarUsuarioAsync(string nome, string email, string senha, string confirmacaoSenha, UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Role", new[] { "A role informada é inválida." } }
            });
        }

        if (senha != confirmacaoSenha)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Password", new[] { "A senha e a confirmação de senha não coincidem." } }
            });
        }

        nome = nome?.Trim() ?? string.Empty;

        if (nome.Length == 0 || nome.Length > TamanhoMaximoDoNome)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Nome", new[] { nome.Length == 0 ? "O nome é obrigatório." : "O nome deve ter no máximo 100 caracteres." } }
            });
        }

        var user = new IdentityUser { UserName = email, Email = email };

        var result = await _userManager.CreateAsync(user, senha);

        if (result.Succeeded == false)
        {
            var errorList = new Dictionary<string, string[]>();

            foreach (var error in result.Errors.Where(e => !CodigosDeUserName.Contains(e.Code)))
            {
                if (errorList.TryGetValue(error.Code, out var existingErrors))
                {
                    errorList[error.Code] = existingErrors.Concat(new[] { error.Description }).ToArray();
                }
                else
                {
                    errorList.Add(error.Code, new[] { error.Description });
                }
            }

            throw new ValidationException(errorList);
        }

        try
        {
            var resultadoRole = await _userManager.AddToRoleAsync(user, role.ToString());

            if (!resultadoRole.Succeeded)
            {
                var erros = string.Join("; ", resultadoRole.Errors.Select(e => $"{e.Code}: {e.Description}"));
                throw new InvalidOperationException($"Não foi possível atribuir a role {role} ao usuário: {erros}");
            }

            if (role == UserRole.Student)
            {
                await _studentRepository.AddAsync(new Student
                {
                    Nome = nome,
                    Email = email,
                    IdentityUserId = user.Id,
                    IdentityUser = user,
                    DataCadastro = DateTime.UtcNow,
                    IsDeleted = false,
                    Enrollments = new List<Enrollment>()
                });
            }
        }
        catch (Exception erroOriginal)
        {
            await DesfazerCriacaoDoUsuarioAsync(user, erroOriginal);
            throw;
        }

        return user;
    }

    private async Task DesfazerCriacaoDoUsuarioAsync(IdentityUser user, Exception erroOriginal)
    {
        IdentityResult resultado;

        try
        {
            resultado = await _userManager.DeleteAsync(user);
        }
        catch (Exception erroNaExclusao)
        {
            throw new AggregateException("Falha ao criar o usuário e ao desfazer a criação.", erroOriginal, erroNaExclusao);
        }

        if (!resultado.Succeeded)
        {
            var erros = string.Join("; ", resultado.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new AggregateException($"Falha ao criar o usuário e ao desfazer a criação: {erros}", erroOriginal);
        }
    }

    public async Task<SessaoEmitidaDto> LoginAsync(LoginInputDto input)
    {
        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user == null)
        {
            throw new UnauthorizedException(MensagemDeCredenciaisInvalidas);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException(MensagemDeCredenciaisInvalidas);
        }

        var roles = await _userManager.GetRolesAsync(user);

        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var (refreshToken, refreshTokenExpiraEm) = await AbrirSessaoAsync(user.Id);

        return new SessaoEmitidaDto(tokenProcess.AccessToken, tokenProcess.ExpiresAt, refreshToken, refreshTokenExpiraEm);
    }

    public async Task<SessaoEmitidaDto> RefreshAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedException(MensagemDeRefreshInvalido);

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var token = await _context.RefreshTokensDeSessao
            .Include(t => t.Sessao)
            .SingleOrDefaultAsync(t => t.Hash == hash);

        if (token is null || token.Sessao.RevogadaEm is not null)
            throw new UnauthorizedException(MensagemDeRefreshInvalido);

        var userId = token.Sessao.UsuarioId;

        if (token.UsadoEm is not null)
            await RevogarSessaoENegarAsync(token.SessaoId, MotivoDaRevogacao.Reuso);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedException(MensagemDeRefreshInvalido);

        if (await _userManager.IsLockedOutAsync(user))
        {
            await RevogarSessoesDoUsuarioAsync(userId, MotivoDaRevogacao.ContaBloqueada);
            throw new UnauthorizedException(MensagemDeRefreshInvalido);
        }

        var agora = DateTime.UtcNow;
        if (token.ExpiraEm <= agora)
            await RevogarSessaoENegarAsync(token.SessaoId, MotivoDaRevogacao.Expirada);

        var marcados = await _context.RefreshTokensDeSessao
            .Where(t => t.Id == token.Id && t.UsadoEm == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsadoEm, agora));

        if (marcados == 0)
            await RevogarSessaoENegarAsync(token.SessaoId, MotivoDaRevogacao.Reuso);

        await _context.RefreshTokensDeSessao
            .Where(t => t.SessaoId == token.SessaoId && t.ExpiraEm < agora)
            .ExecuteDeleteAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var (novoRefreshToken, novoRefreshTokenExpiraEm) = await EmitirRefreshTokenAsync(token.SessaoId);

        return new SessaoEmitidaDto(tokenProcess.AccessToken, tokenProcess.ExpiresAt, novoRefreshToken, novoRefreshTokenExpiraEm);
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var sessaoId = await _context.RefreshTokensDeSessao
            .Where(t => t.Hash == hash)
            .Select(t => (Guid?)t.SessaoId)
            .SingleOrDefaultAsync();

        if (sessaoId is not null)
            await RevogarSessaoAsync(sessaoId.Value, MotivoDaRevogacao.Logout);
    }

    private async Task<(string Valor, DateTime ExpiraEm)> AbrirSessaoAsync(string usuarioId)
    {
        var agora = DateTime.UtcNow;

        await _context.SessoesDeAutenticacao
            .Where(s => s.UsuarioId == usuarioId
                && (s.RevogadaEm != null || s.RefreshTokens.All(t => t.ExpiraEm <= agora)))
            .ExecuteDeleteAsync();

        var sessao = new SessaoDeAutenticacao { Id = Guid.NewGuid(), UsuarioId = usuarioId, CriadaEm = agora };
        _context.SessoesDeAutenticacao.Add(sessao);
        await _context.SaveChangesAsync();

        return await EmitirRefreshTokenAsync(sessao.Id);
    }

    private async Task<(string Valor, DateTime ExpiraEm)> EmitirRefreshTokenAsync(Guid sessaoId)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiraEm = _tokenService.GetRefreshTokenExpiration();

        _context.RefreshTokensDeSessao.Add(new RefreshTokenDaSessao
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            Hash = _tokenService.HashRefreshToken(refreshToken),
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = expiraEm
        });
        await _context.SaveChangesAsync();

        return (refreshToken, expiraEm);
    }

    private async Task RevogarSessaoENegarAsync(Guid sessaoId, string motivo)
    {
        await RevogarSessaoAsync(sessaoId, motivo);
        throw new UnauthorizedException(MensagemDeRefreshInvalido);
    }

    private Task RevogarSessaoAsync(Guid sessaoId, string motivo) =>
        _context.SessoesDeAutenticacao
            .Where(s => s.Id == sessaoId && s.RevogadaEm == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevogadaEm, DateTime.UtcNow)
                .SetProperty(x => x.MotivoDaRevogacao, motivo));

    private Task RevogarSessoesDoUsuarioAsync(string usuarioId, string motivo) =>
        _context.SessoesDeAutenticacao
            .Where(s => s.UsuarioId == usuarioId && s.RevogadaEm == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevogadaEm, DateTime.UtcNow)
                .SetProperty(x => x.MotivoDaRevogacao, motivo));
}
