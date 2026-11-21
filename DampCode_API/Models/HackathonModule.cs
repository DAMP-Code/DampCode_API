namespace DampCode_API.Models;

// Módulo do material de apoio do Hackathon. Position começa em 1 e não tem buracos (o serviço reordena ao apagar).
public sealed class HackathonModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HackathonId { get; set; }
    public int Position { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Hackathon Hackathon { get; set; } = null!;
    public ICollection<HackathonModuleContent> Contents { get; set; } = [];
}

// Conteúdo de um módulo: texto (Body) ou link/vídeo (Url, com Body opcional como descrição curta).
public sealed class HackathonModuleContent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public int Position { get; set; }
    public HackathonModuleContentType ContentType { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public HackathonModule Module { get; set; } = null!;
}

public enum HackathonModuleContentType { Text, Link, Video }
