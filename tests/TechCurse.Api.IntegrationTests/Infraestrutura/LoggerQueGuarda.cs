using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class LoggerQueGuarda<T> : ILogger<T>
{
    public sealed record Evento(LogLevel Nivel, string Mensagem, Exception? Excecao);

    public ConcurrentQueue<Evento> Eventos { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Eventos.Enqueue(new Evento(logLevel, formatter(state, exception), exception));
    }
}
