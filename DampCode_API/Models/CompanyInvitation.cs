namespace DampCode_API.Models;

// Convite de funcionário. Expira 24h após a criação; a expiração é calculada por ExpiresAt, não gravada no status.
public sealed class CompanyInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid InvitedByAccountId { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public string? JobTitle { get; set; }
    public CompanyInvitationStatus Status { get; set; } = CompanyInvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Company Company { get; set; } = null!;
    public Account InvitedByAccount { get; set; } = null!;
}

public enum CompanyInvitationStatus { Pending, Accepted, Declined, Revoked }
