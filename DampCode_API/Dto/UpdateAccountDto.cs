namespace DampCode_API.Dto;

// Nome nulo fica como está; cor nula ou vazia volta para a cor padrão.
// Foto nula fica como está; vazia remove a foto.
public sealed class UpdateAccountDto
{
    public string? Name { get; set; }
    public string? AvatarColor { get; set; }
    public string? AvatarImage { get; set; }
}
