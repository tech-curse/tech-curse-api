using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Infrastructure.Identity;

public class TokenService : ITokenService
{
    private const int TamanhoDoRefreshTokenEmBytes = 64;

    private const int DiasDeValidadeDoRefreshTokenPadrao = 7;

    private const int MinutosDeValidadeDoAccessTokenPadrao = 15;

    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue("Jwt:AccessTokenMinutes", MinutosDeValidadeDoAccessTokenPadrao));

        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]!);
        var credential = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = credential,
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new TokenOutputDto(tokenHandler.WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoDoRefreshTokenEmBytes));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToBase64String(hash);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        var dias = _configuration.GetValue("Jwt:RefreshTokenDays", DiasDeValidadeDoRefreshTokenPadrao);

        return DateTime.UtcNow.AddDays(dias);
    }
}
