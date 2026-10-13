using TechCurse.Application.DTOs;

namespace TechCurse.Application.Interfaces;

public interface IAuthService
{
    Task RegisterAsync(RegisterInputDto input);
    Task CreateUserAsync(CreateUserInputDto input);
    Task<SessaoEmitidaDto> LoginAsync(LoginInputDto input);
    Task<SessaoEmitidaDto> RefreshAsync(string? refreshToken);
    Task LogoutAsync(string? refreshToken);
}
