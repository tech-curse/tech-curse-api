using TechCurse.Domain.Enums;

namespace TechCurse.Application.Interfaces;

public interface ICurrentUserService
{
    string? GetUserId();
    string? GetUserEmail();
    bool IsInRole(UserRole roleName);
}
