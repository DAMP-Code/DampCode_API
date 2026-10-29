namespace DampCode_API.Dto;

// Campos nulos ficam como estão.
public sealed class UpdateTeamDto
{
    public string? Nome { get; set; }
    public bool? Aberta { get; set; }
}
