namespace DampCode_API.Models;

public sealed class userProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid accountId { get; set; }

    public required string name { get; set; }

    public string? biography { get; set; }

    public int level { get; set; } = 1;

    public decimal xp { get; set; } = 0;

    public string? avatarStorageKey { get; set; }

    public ProfileVisibility profileVisibility { get; set; }
        = ProfileVisibility.Public;

    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    public DateTime updatedAt { get; set; } = DateTime.UtcNow;

    // Autenticação e identidade.
    public accounts account { get; set; } = null!;

    // Histórico completo de associações empresariais.
    public ICollection<companyMembership> companyMemberships { get; set; }
        = new List<companyMembership>();

    // Tecnologias dominadas/declaradas pelo usuário.
    public ICollection<userTechnology> technologies { get; set; }
        = new List<userTechnology>();

    // Experiências profissionais apresentadas no perfil.
    public ICollection<profileExperience> profileExperiences { get; set; }
        = new List<profileExperience>();

    // Participações individuais em Hackathons.
    public ICollection<participation> individualParticipations { get; set; }
        = new List<participation>();

    // Histórico de integração em equipes.
    public ICollection<teamMember> teamMemberships { get; set; }
        = new List<teamMember>();

    // Equipes criadas pelo usuário.
    public ICollection<hackathonTeam> createdTeams { get; set; }
        = new List<hackathonTeam>();
}

public enum ProfileVisibility
{
    Public,
    Private
}