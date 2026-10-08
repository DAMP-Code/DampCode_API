namespace DampCode_API.Models;

public sealed class CompanyMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Company Company { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
