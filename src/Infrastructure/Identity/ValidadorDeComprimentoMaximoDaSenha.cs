using Microsoft.AspNetCore.Identity;

namespace TechCurse.Infrastructure.Identity;

public sealed class ValidadorDeComprimentoMaximoDaSenha : IPasswordValidator<IdentityUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password)
    {
        if (password is { Length: > PoliticaDeSenha.ComprimentoMaximo })
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = PoliticaDeSenha.CodigoDeSenhaLongaDemais,
                Description = $"A senha deve ter no máximo {PoliticaDeSenha.ComprimentoMaximo} caracteres."
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
