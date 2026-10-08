namespace DampCode_API.Models;

// Entidade-base preservada para a próxima etapa. Ainda não faz parte do DbContext
// porque publicação e gerenciamento dependem da autorização empresarial.
public sealed class Hackathon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Description { get; set; }
    public required string Area { get; set; }
    public required string ParticipationMode { get; set; }
    public required string Status { get; set; }
    public string? EvaluationMethod { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? ReviewEndsAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? LogoStorageKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
