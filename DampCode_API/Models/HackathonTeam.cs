namespace DampCode_API.Models;

// Equipe de um único Hackathon, com no máximo 5 membros ativos.
// MemberCount é token de concorrência: dois aceites na última vaga não passam juntos.
public sealed class HackathonTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HackathonId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public bool IsOpen { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Hackathon Hackathon { get; set; } = null!;
    public ICollection<Participation> Members { get; set; } = [];
    public ICollection<TeamInvitation> Invitations { get; set; } = [];
    public ICollection<TeamJoinRequest> JoinRequests { get; set; } = [];
}

// Convite para a equipe. Expira 24h após a criação, como o convite de funcionário.
public sealed class TeamInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid InvitedByAccountId { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public TeamInvitationStatus Status { get; set; } = TeamInvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public HackathonTeam Team { get; set; } = null!;
    public Account InvitedByAccount { get; set; } = null!;
}

// Pedido de um participante para entrar em uma equipe aberta.
public sealed class TeamJoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid AccountId { get; set; }
    public TeamJoinRequestStatus Status { get; set; } = TeamJoinRequestStatus.Pending;
    public Guid? RespondedByAccountId { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public HackathonTeam Team { get; set; } = null!;
    public Account Account { get; set; } = null!;
}

public enum TeamInvitationStatus { Pending, Accepted, Declined, Revoked, Cancelled }
public enum TeamJoinRequestStatus { Pending, Accepted, Declined, Cancelled }
