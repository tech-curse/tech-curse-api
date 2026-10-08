using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private const string ProvedorDeLogin = "JWTApp";

    private const string NomeDoRefreshToken = "RefreshToken";

    private const string NomeDaExpiracaoDoRefreshToken = "RefreshTokenExpiry";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IStudentRepository _studentRepository;

    public AuthService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ITokenService tokenService, IStudentRepository studentRepository)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _studentRepository = studentRepository;
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

        if (role == UserRole.Student && await _userManager.FindByEmailAsync(email) is null && await _studentRepository.EmailExistsAsync(email))
        {
            throw new ConflictException("Já existe um perfil de estudante com este e-mail.");
        }

        var user = new IdentityUser { UserName = nome, Email = email };

        var result = await _userManager.CreateAsync(user, senha);

        if (result.Succeeded == false)
        {
            var errorList = new Dictionary<string, string[]>();

            foreach (var error in result.Errors)
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

    public async Task<AuthOutputDto?> LoginAsync(LoginInputDto input)
    {
        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user == null)
        {
            throw new UnauthorizedException("E-mail ou senha incorretos.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Usuário não autenticado.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = await EmitirRefreshTokenAsync(user);

        return new AuthOutputDto(tokenProcess.AccessToken, refreshToken, tokenProcess.ExpiresAt);
    }

    public async Task<AuthOutputDto?> RefreshAsync(RefreshTokenInputDto input)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(input.AccessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            throw new UnauthorizedException("Refresh Token inválido ou expirado.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("Usuário não encontrado");

        var hashPersistido = await _userManager.GetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDoRefreshToken);

        if (!await RefreshTokenEstaVigenteAsync(user))
        {
            await RevogarRefreshTokenAsync(user);

            throw new UnauthorizedException("Refresh Token inválido ou expirado.");
        }

        if (!_tokenService.RefreshTokenMatches(input.RefreshToken, hashPersistido))
        {
            throw new UnauthorizedException("Refresh Token inválido ou expirado.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = await EmitirRefreshTokenAsync(user);

        return new AuthOutputDto(tokenProcess.AccessToken, refreshToken, tokenProcess.ExpiresAt);
    }

    private async Task<string> EmitirRefreshTokenAsync(IdentityUser user)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiraEm = _tokenService.GetRefreshTokenExpiration();

        await _userManager.SetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDoRefreshToken, _tokenService.HashRefreshToken(refreshToken));

        await _userManager.SetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken,
            expiraEm.ToString("O", CultureInfo.InvariantCulture));

        return refreshToken;
    }

    private async Task<bool> RefreshTokenEstaVigenteAsync(IdentityUser user)
    {
        var expiracaoPersistida = await _userManager.GetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken);

        if (string.IsNullOrEmpty(expiracaoPersistida))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                expiracaoPersistida,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiraEm))
        {
            return false;
        }

        return expiraEm > DateTimeOffset.UtcNow;
    }

    private async Task RevogarRefreshTokenAsync(IdentityUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, ProvedorDeLogin, NomeDoRefreshToken);
        await _userManager.RemoveAuthenticationTokenAsync(user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken);
    }
}
