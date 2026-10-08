namespace TechCurse.Domain.Exceptions;

public class GatewayTimeoutException : Exception
{
    public GatewayTimeoutException(string message) : base(message)
    {
    }
}
