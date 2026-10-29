using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class UpdateVerificationDto
{
    [Required]
    public required string Status { get; set; }
}
