namespace DampCode_API.Models;

public sealed class submission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid participationId { get; set; }

    public Guid stageId { get; set; }

    public required string status { get; set; }

    public int currentVersion { get; set; }

    public string? currentMongoVersionId { get; set; }

    public DateTime? firstSubmittedAt { get; set; }

    public DateTime? lastSubmittedAt { get; set; }

    public DateTime? finalizedAt { get; set; }

    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    // Participação responsável pela entrega.
    public participation participation { get; set; } = null!;

    // Etapa à qual a entrega pertence.
    public hackathonStage stage { get; set; } = null!;

    // Resultados das avaliações desta submissão.
    public ICollection<evaluationResult> evaluationResults { get; set; }
        = new List<evaluationResult>();
}