using Microsoft.AspNetCore.Identity;

namespace TechCurse.Infrastructure.Identity;

public static class PoliticaDeSenha
{
    public const int ComprimentoMinimo = 12;
    public const int ComprimentoMaximo = 128;
    public const string CodigoDeSenhaLongaDemais = "PasswordTooLong";

    public static void Aplicar(PasswordOptions opcoes)
    {
        opcoes.RequiredLength = ComprimentoMinimo;
        opcoes.RequireLowercase = true;
        opcoes.RequireUppercase = true;
        opcoes.RequireDigit = true;
        opcoes.RequireNonAlphanumeric = true;
    }
}
