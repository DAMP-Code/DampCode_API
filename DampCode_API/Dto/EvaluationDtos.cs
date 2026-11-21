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

// Resultado publicado visto pelo participante; sem entrega, Posicao e Nota ficam nulas.
public sealed record ParticipationResultDto(int? Posicao, int TotalClassificados, int? Nota, string? Comentario, PrizeDto? Premio);

// Linha da classificação pública: sem comentário da empresa, entrega, código, e-mail ou id de conta.
public sealed record HackathonResultEntryDto(int Posicao, string Nome, bool Equipe, IReadOnlyList<string> Membros, int? Nota, PrizeDto? Premio, bool Voce);

// Resultado público do Hackathon; antes da publicação a classificação vem vazia.
public sealed record HackathonResultDto(
    Guid HackathonId,
    string Titulo,
    bool ResultadoPublicado,
    int TotalClassificados,
    IReadOnlyList<HackathonResultEntryDto> Classificacao);
