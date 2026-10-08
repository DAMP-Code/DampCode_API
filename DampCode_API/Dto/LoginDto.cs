using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class LoginDto
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; set; }
    [Required, StringLength(128, MinimumLength = 1)]
    public required string Password { get; set; }
}
