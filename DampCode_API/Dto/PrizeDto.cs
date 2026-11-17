namespace DampCode_API.Dto;

// Prêmio de uma posição, usado na entrada (criação) e na saída do Hackathon.
public sealed class PrizeDto
{
    public int Posicao { get; set; }

    public string? Tipo { get; set; }

    public decimal? Valor { get; set; }

    public string? Descricao { get; set; }
}
