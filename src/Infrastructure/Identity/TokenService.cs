using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Infrastructure.Identity;

public class TokenService : ITokenService
{
    private const int TamanhoDoRefreshTokenEmBytes = 64;

    private const int TamanhoDoHashEmBytes = 32;

    private const int DiasDeValidadeDoRefreshTokenPadrao = 7;

    private const string MensagemDeRefreshInvalido = "Refresh Token inválido ou expirado.";

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

        var expiresAt = DateTime.UtcNow.AddHours(2);

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

    public bool RefreshTokenMatches(string refreshToken, string? storedHash)
    {
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        Span<byte> hashPersistido = stackalloc byte[TamanhoDoHashEmBytes];

        if (!Convert.TryFromBase64String(storedHash, hashPersistido, out var bytesEscritos)
            || bytesEscritos != TamanhoDoHashEmBytes)
        {
            return false;
        }

        Span<byte> hashRecebido = stackalloc byte[TamanhoDoHashEmBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken), hashRecebido);

        return CryptographicOperations.FixedTimeEquals(hashRecebido, hashPersistido);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        var dias = _configuration.GetValue("Jwt:RefreshTokenDays", DiasDeValidadeDoRefreshTokenPadrao);

        return DateTime.UtcNow.AddDays(dias);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]!);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateLifetime = false,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        ClaimsPrincipal principal;
        SecurityToken securityToken;

        try
        {
            principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
        }
        catch (Exception erro) when (erro is SecurityTokenException or ArgumentException)
        {
            throw new UnauthorizedException(MensagemDeRefreshInvalido);
        }

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new UnauthorizedException(MensagemDeRefreshInvalido);
        }

        return principal;
    }
}
