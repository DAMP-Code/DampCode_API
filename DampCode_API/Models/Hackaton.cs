using MongoDB.Driver;

namespace DampCode_API.Models;

public sealed class hackathon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid companyId { get; set; }

    public Guid createdByAccountId { get; set; }

    public required string title { get; set; }

    public required string slug { get; set; }

    public required string description { get; set; }

    public required string area { get; set; }

    public required string participationMode { get; set; }

    public required string status { get; set; }

    public string? evaluationMethod { get; set; }

    public DateTime? startsAt { get; set; }

    public DateTime? endsAt { get; set; }

    public DateTime? reviewEndsAt { get; set; }

    public DateTime? scheduledAt { get; set; }

    public DateTime? closedAt { get; set; }

    public DateTime? cancelledAt { get; set; }

    public string? primaryColor { get; set; }

    public string? secondaryColor { get; set; }

    public string? backgroundColor { get; set; }

    public string? logoStorageKey { get; set; }

    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? deletedAt { get; set; }

    // Empresa proprietária do Hackathon.
    public company company { get; set; } = null!;

    // Conta responsável pela criação.
    public accounts createdByAccount { get; set; } = null!;

    // Tecnologias utilizadas/classificadoras do Hackathon.
    public ICollection<hackathonTechnology> technologies { get; set; }
        = new List<hackathonTechnology>();

    // Etapas ordenadas do Hackathon.
    public ICollection<hackathonStage> stages { get; set; }
        = new List<hackathonStage>();

    // Prêmios oferecidos.
    public ICollection<prize> prizes { get; set; }
        = new List<prize>();

    // Equipes criadas especificamente para este Hackathon.
    public ICollection<hackathonTeam> teams { get; set; }
        = new List<hackathonTeam>();

    // Inscrições oficiais.
    public ICollection<participation> participations { get; set; }
        = new List<participation>();

    // Solicitações de cancelamento.
    public ICollection<cancellationRequest> cancellationRequests { get; set; }
        = new List<cancellationRequest>();

    // Eventos que afetaram a reputação da empresa relacionados ao Hackathon.
    public ICollection<companyReputationEvent> reputationEvents { get; set; }
        = new List<companyReputationEvent>();
}