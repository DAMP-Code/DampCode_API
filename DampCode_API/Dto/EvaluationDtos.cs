namespace DampCode_API.Dto;

// Nota e comentário de uma entrega. As regras (0-100, 1000 caracteres) ficam no serviço.
public sealed class EvaluateDto
{
    public int? Nota { get; set; }
    public string? Comentario { get; set; }
}

public sealed class WinnerDto
{
    public int Posicao { get; set; }
    public Guid SubmissionId { get; set; }
}

// Substitui a escolha toda de uma vez: uma entrega por posição premiada.
public sealed class SetWinnersDto
{
    public List<WinnerDto>? Vencedores { get; set; }
}

public sealed record EvaluationEntryDto(
    Guid SubmissionId,
    string Nome,
    string? Linguagem,
    int? Semelhanca,
    int? Nota,
    string? Comentario,
    int? PosicaoPremio,
    DateTime? EnviadoEm);

public sealed record EvaluationRankingDto(
    bool PodeAvaliar,
    bool ResultadoPublicado,
    IReadOnlyList<PrizeDto> Premios,
    IReadOnlyList<EvaluationEntryDto> Entregas);
