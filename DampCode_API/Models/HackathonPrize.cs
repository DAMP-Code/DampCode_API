namespace DampCode_API.Models;

// Prêmio de uma posição (1º, 2º...) do Hackathon: dinheiro (Amount) ou entrevista (Description opcional).
public sealed class HackathonPrize
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HackathonId { get; set; }
    public int Position { get; set; }
    public HackathonRewardType RewardType { get; set; }
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Hackathon Hackathon { get; set; } = null!;
}
