namespace DampCode_API.Models;

// Hackathon fica no PostgreSQL, ligado à empresa que o criou. O Status gravado é só
// Draft, Scheduled, Closed (encerramento manual) ou Cancelled; as outras fases são calculadas pelas datas.
public sealed class Hackathon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public required string Area { get; set; }
    public HackathonParticipationMode ParticipationMode { get; set; }
    public HackathonStatus Status { get; set; } = HackathonStatus.Draft;
    // Valores (quiz, links, arquivos, executor) ainda não definidos.
    public string? EvaluationMethod { get; set; }
    public decimal? PrizeAmount { get; set; }
    public HackathonRewardType? RewardType { get; set; }
    public int? WinnersCount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? ReviewEndsAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    // Data URL enquanto não existe Object Storage; LogoStorageKey fica reservado para ele.
    public string? Logo { get; set; }
    public string? LogoStorageKey { get; set; }
    // Solução de referência: só a empresa vê, para comparar com as entregas.
    public string? ReferenceLanguage { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public Company Company { get; set; } = null!;
    public ICollection<HackathonTechnology> Technologies { get; set; } = [];
    public ICollection<HackathonTeam> Teams { get; set; } = [];
    public ICollection<Participation> Participations { get; set; } = [];
    public ICollection<Submission> Submissions { get; set; } = [];
}

public sealed class HackathonTechnology
{
    public Guid HackathonId { get; set; }
    public Guid TechnologyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Hackathon Hackathon { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}

public enum HackathonParticipationMode { Individual, Team }
public enum HackathonStatus { Draft, Scheduled, Active, InReview, Closed, Cancelled }
public enum HackathonRewardType { Money, Interview }
