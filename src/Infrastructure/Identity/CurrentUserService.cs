using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;

namespace TechCurse.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public string? GetUserEmail()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.FindFirstValue(ClaimTypes.Email);
    }

    public bool IsInRole(UserRole roleName)
        => _httpContextAccessor.HttpContext?.User?.IsInRole(roleName.ToString()) ?? false;
}
