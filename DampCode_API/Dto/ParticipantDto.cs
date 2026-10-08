using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class ParticipantDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public required string Name { get; set; }
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; set; }
    [Required, StringLength(128, MinimumLength = 10)]
    public required string Password { get; set; }
    public List<string> Tecnologias { get; set; } = [];
}
