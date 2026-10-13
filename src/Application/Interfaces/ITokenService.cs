using Microsoft.AspNetCore.Identity;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Interfaces;

public interface ITokenService
{
    TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    DateTime GetRefreshTokenExpiration();
}
