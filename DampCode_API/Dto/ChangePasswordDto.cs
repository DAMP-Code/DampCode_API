using System.ComponentModel.DataAnnotations;

namespace DampCode_API.Dto;

// Tamanho e senha atual são conferidos no serviço, para a mensagem sair no Detail.
public sealed class ChangePasswordDto
{
    [Required]
    public required string SenhaAtual { get; set; }
    [Required]
    public required string NovaSenha { get; set; }
}
