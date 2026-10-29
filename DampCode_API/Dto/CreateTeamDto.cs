namespace DampCode_API.Dto;

// Nome e "aceitar pedidos" da equipe; o limite de 100 caracteres é validado no serviço com a mensagem do design.
public sealed class CreateTeamDto
{
    public string? Nome { get; set; }
    public bool Aberta { get; set; }
}
