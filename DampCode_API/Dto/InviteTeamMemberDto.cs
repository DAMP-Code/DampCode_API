using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class InviteTeamMemberDto
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; set; }
}
