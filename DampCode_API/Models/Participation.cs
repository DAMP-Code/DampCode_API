namespace DampCode_API.Models;

// Inscrição de uma pessoa em um Hackathon (individual ou dentro de uma equipe).
// Sair grava LeftAt para preservar o histórico, como CompanyMembership.EndedAt.
public sealed class Participation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HackathonId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? TeamId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Hackathon Hackathon { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public HackathonTeam? Team { get; set; }
}
