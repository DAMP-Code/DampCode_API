namespace DampCode_API.Models;

// Entrega de um participante individual (ParticipationId) ou de uma equipe (TeamId) em um Hackathon.
// O código de cada versão fica no MongoDB; a lista de versões (SubmissionVersion) fica no PostgreSQL.
public sealed class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HackathonId { get; set; }

    public Guid? ParticipationId { get; set; }

    public Guid? TeamId { get; set; }

    // Reservado para etapas do Hackathon, que ainda não existem.
    public Guid? StageId { get; set; }


    public required string Status { get; set; }

    public int CurrentVersion { get; set; }

    public string? CurrentMongoVersionId { get; set; }

    public DateTime? FirstSubmittedAt { get; set; }

    public DateTime? LastSubmittedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Hackathon Hackathon { get; set; } = null!;

    public Participation? Participation { get; set; }

    public HackathonTeam? Team { get; set; }

    public ICollection<SubmissionVersion> Versions { get; set; } = [];
}
