namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public abstract class TesteDeIntegracao(AmbienteDeTeste ambiente) : IAsyncLifetime
{
    protected AmbienteDeTeste Ambiente { get; } = ambiente;

    protected HttpClient Cliente { get; } = ambiente.Fabrica.CreateClient();

    protected static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => Ambiente.RestaurarEstadoAsync();

    public ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        return ValueTask.CompletedTask;
    }
}
