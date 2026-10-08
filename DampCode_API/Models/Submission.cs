namespace DampCode_API.Models;

// Entidade-base preservada para a próxima etapa. O conteúdo versionado da
// submissão continuará destinado ao MongoDB/Object Storage.
public sealed class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ParticipationId { get; set; }

    public Guid StageId { get; set; }


    public required string Status { get; set; }

    public int CurrentVersion { get; set; }

    public string? CurrentMongoVersionId { get; set; }

    public DateTime? FirstSubmittedAt { get; set; }

    public DateTime? LastSubmittedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
