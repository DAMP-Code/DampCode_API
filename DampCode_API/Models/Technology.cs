namespace DampCode_API.Models;

public sealed class Technology
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserTechnology> Users { get; set; } = [];
    public ICollection<CompanyTechnology> Companies { get; set; } = [];
}

public sealed class UserTechnology
{
    public Guid UserProfileId { get; set; }
    public Guid TechnologyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserProfile UserProfile { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}

public sealed class CompanyTechnology
{
    public Guid CompanyId { get; set; }
    public Guid TechnologyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Company Company { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}
