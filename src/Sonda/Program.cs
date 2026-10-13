var endereco = args.Length > 0 ? args[0] : "http://127.0.0.1:8080/health/ready";

using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

try
{
    using var resposta = await cliente.GetAsync(endereco);
    return resposta.IsSuccessStatusCode ? 0 : 1;
}
catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"Sonda: {erro.Message}");
    return 1;
}
