using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

public sealed class EmpresaDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public required string Name { get; set; }
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; set; }
    [Required, StringLength(128, MinimumLength = 10)]
    public required string Password { get; set; }
    [Required, RegularExpression(@"^\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}$")]
    public required string Cnpj { get; set; }
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Area { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)]
    public required string Descricao { get; set; }
    public List<string> Tecnologias { get; set; } = [];
}
