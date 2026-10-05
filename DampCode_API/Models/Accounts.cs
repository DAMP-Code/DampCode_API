//BRANCH Diego
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DampCode_API.Models;

public sealed class accounts
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string email { get; set; }

    public required string normalizedEmail { get; set; }

    public required string passwordHash { get; set; }

    public required string accountType { get; set; }

    public required string status { get; set; }

    public DateTime? emailVerifiedAt { get; set; }

    public DateTime? credentialsChangedAt { get; set; }

    public DateTime? lastLoginAt { get; set; }

    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? deletedAt { get; set; }

    // Perfil da pessoa associado à conta.
    public studentData? studentData { get; set; }

    // Empresa cuja conta é a conta principal.
    public company? primaryCompany { get; set; }

    // Papéis administrativos recebidos pela conta.
    public ICollection<accountSystemRole> accountSystemRoles { get; set; }
        = new List<accountSystemRole>();

    // Papéis administrativos concedidos por esta conta.
    public ICollection<accountSystemRole> rolesGrantedByMe { get; set; }
        = new List<accountSystemRole>();

    // Solicitações de alteração de e-mail realizadas pela conta.
    public ICollection<emailChangeRequest> emailChangeRequests { get; set; }
        = new List<emailChangeRequest>();

    // Empresas que tiveram verificações revisadas por esta conta.
    public ICollection<companyVerification> companyVerificationsReviewed { get; set; }
        = new List<companyVerification>();

    // Hackathons criados por esta conta.
    public ICollection<hackathon> createdHackathons { get; set; }
        = new List<hackathon>();

    // Solicitações de cancelamento realizadas pela conta.
    public ICollection<cancellationRequest> cancellationRequests { get; set; }
        = new List<cancellationRequest>();

    // Solicitações de cancelamento revisadas pela conta.
    public ICollection<CancellationRequest> cancellationRequestsReviewed { get; set; }
        = new List<cancellationRequest>();

    // Convites empresariais enviados pela conta.
    public ICollection<companyInvitation> companyInvitationsSent { get; set; }
        = new List<companyInvitation>();

    // Vínculos empresariais removidos por esta conta.
    public ICollection<companyMembership> removedCompanyMemberships { get; set; }
        = new List<companyMembership>();

    // Eventos de reputação criados pela conta.
    public ICollection<companyReputationEvent> reputationEventsCreated { get; set; }
        = new List<companyReputationEvent>();

    // Avaliações realizadas pela conta.
    public ICollection<evaluationResult> evaluationResults { get; set; }
        = new List<evaluationResult>();
}