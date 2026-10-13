using Microsoft.AspNetCore.Identity;

namespace TechCurse.Infrastructure.Identity;

public sealed class DescritorDeErrosDoIdentity : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Erro(nameof(DefaultError), "Não foi possível concluir a operação.");

    public override IdentityError PasswordTooShort(int length) =>
        Erro(nameof(PasswordTooShort), $"A senha deve ter no mínimo {length} caracteres.");

    public override IdentityError PasswordRequiresLower() =>
        Erro(nameof(PasswordRequiresLower), "A senha deve ter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Erro(nameof(PasswordRequiresUpper), "A senha deve ter pelo menos uma letra maiúscula.");

    public override IdentityError PasswordRequiresDigit() =>
        Erro(nameof(PasswordRequiresDigit), "A senha deve ter pelo menos um número.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha deve ter pelo menos um símbolo (caractere que não é letra nem número).");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Erro(nameof(PasswordRequiresUniqueChars), $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes.");

    public override IdentityError DuplicateEmail(string email) =>
        Erro(nameof(DuplicateEmail), "Este e-mail já está em uso.");

    public override IdentityError InvalidEmail(string? email) =>
        Erro(nameof(InvalidEmail), "Informe um e-mail válido.");

    public override IdentityError PasswordMismatch() =>
        Erro(nameof(PasswordMismatch), "Senha incorreta.");

    private static IdentityError Erro(string codigo, string descricao) => new() { Code = codigo, Description = descricao };
}
