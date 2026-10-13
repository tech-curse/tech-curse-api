namespace TechCurse.Infrastructure.Identity.Sessoes;

public class RefreshTokenDaSessao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public SessaoDeAutenticacao Sessao { get; set; } = null!;
    public string Hash { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadoEm { get; set; }
}
