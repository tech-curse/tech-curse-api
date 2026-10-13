namespace TechCurse.Infrastructure.Identity.Sessoes;

public class SessaoDeAutenticacao
{
    public Guid Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime CriadaEm { get; set; }
    public DateTime? RevogadaEm { get; set; }
    public string? MotivoDaRevogacao { get; set; }
    public List<RefreshTokenDaSessao> RefreshTokens { get; set; } = [];
}
