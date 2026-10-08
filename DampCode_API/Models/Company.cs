namespace DampCode_API.Models;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrimaryAccountId { get; set; }
    public required string Name { get; set; }
    public required string Cnpj { get; set; }
    public string? Area { get; set; }
    public string? Description { get; set; }
    public CompanyVerificationStatus VerificationStatus { get; set; } = CompanyVerificationStatus.Pending;
    public decimal ReputationScore { get; set; } = 5m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public Account PrimaryAccount { get; set; } = null!;
    public ICollection<CompanyMembership> Memberships { get; set; } = [];
    public ICollection<CompanyTechnology> Technologies { get; set; } = [];
}

public enum CompanyVerificationStatus { Pending, Verified, Rejected }
