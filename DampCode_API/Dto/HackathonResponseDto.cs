namespace DampCode_API.Dto;

// Mantém os nomes de JSON que o front já lê (hackathonId, titulo, empresa, status, dataFinal...).
public sealed record HackathonResponseDto(
    string HackathonId,
    string Titulo,
    string Descricao,
    string Area,
    string Empresa,
    IReadOnlyList<string> Tecnologias,
    string? Metodo,
    string? Ranking,
    decimal Premiacao,
    string? CorPrincipal,
    string? CorSecundaria,
    string? CorFundo,
    string? Logo,
    DateTime DataCriacao,
    DateTime? DataFinal,
    bool Status,
    string Participantes,
    string Fase,
    DateTime? DataInicio,
    DateTime? DataFimAvaliacao,
    string Formato,
    string? TipoPremiacao,
    Guid CompanyId);
