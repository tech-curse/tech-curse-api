namespace TechCurse.Domain.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }
    public ValidationException(IDictionary<string, string[]> errors) : base("Ocorreram um ou mais erros de validação.")
    {
        Errors = errors;
    }
}
