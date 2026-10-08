namespace DampCode_API.Models;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public AccountType AccountType { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? CredentialsChangedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public UserProfile? UserProfile { get; set; }
    public Company? PrimaryCompany { get; set; }
    public ICollection<CompanyMembership> CompanyMemberships { get; set; } = [];
}

public enum AccountType { Participant, Company, Admin }
public enum AccountStatus { Active, Suspended, Disabled }
