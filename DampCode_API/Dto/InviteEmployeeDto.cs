using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class InviteEmployeeDto
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; set; }
    [StringLength(150)]
    public string? JobTitle { get; set; }
}
