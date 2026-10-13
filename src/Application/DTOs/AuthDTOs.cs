using TechCurse.Domain.Enums;

namespace TechCurse.Application.DTOs;

public record RegisterInputDto(string Name, string Email, string Password, string ConfirmPassword);
public record CreateUserInputDto(string Name, string Email, UserRole Role, string Password, string ConfirmPassword);
public record LoginInputDto(string Email, string Password);
public record TokenOutputDto(string AccessToken, DateTime ExpiresAt);
public record AuthOutputDto(string AccessToken, DateTime ExpiresAt);
public record SessaoEmitidaDto(string AccessToken, DateTime ExpiresAt, string RefreshToken, DateTime RefreshTokenExpiraEm);
