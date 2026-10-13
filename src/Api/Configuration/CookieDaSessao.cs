namespace TechCurse.Api.Configuration;

public static class CookieDaSessao
{
    public const string Nome = "__Secure-tech-curse-sessao";
    public const string Caminho = "/tech-curse/Auth";

    public static CookieOptions Opcoes(DateTime? expiraEm) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Caminho,
        IsEssential = true,
        Expires = expiraEm is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiraEm.Value, DateTimeKind.Utc))
    };
}
