namespace DampCode_API.Dto;

// Uma versão nova da entrega. As regras (linguagem, vazio, 100 KB) ficam no serviço, com as mensagens do design.
public sealed class SubmitCodeDto
{
    public string? Linguagem { get; set; }
    public string? Codigo { get; set; }
    public string? NomeArquivo { get; set; }
}
