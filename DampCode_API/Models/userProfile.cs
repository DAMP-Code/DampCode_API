namespace DampCode_API.Models;

public sealed class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public required string Name { get; set; }

    public string? Biography { get; set; }

    public int Level { get; set; } = 1;

    public decimal Xp { get; set; }

    public string? AvatarStorageKey { get; set; }

    public ProfileVisibility ProfileVisibility { get; set; }
        = ProfileVisibility.Public;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Autenticação e identidade.
    public Account Account { get; set; } = null!;
    public ICollection<UserTechnology> Technologies { get; set; } = [];
}

public enum ProfileVisibility
{
    Public,
    Private
}
