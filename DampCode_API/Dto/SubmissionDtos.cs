namespace DampCode_API.Dto;

// Saídas da entrega de código, com os nomes de JSON que o front lê (seção 5.2 do design).
public sealed record SubmissionVersionDto(int Versao, string Linguagem, string? NomeArquivo, string EnviadoPor, DateTime EnviadoEm, bool Vale);

public sealed record SubmissionCodeDto(int Versao, string Linguagem, string? NomeArquivo, string Codigo);

public sealed record MySubmissionDto(
    bool Aberta,
    DateTime? DataInicio,
    DateTime? DataFinal,
    string? Equipe,
    IReadOnlyList<SubmissionVersionDto> Versoes,
    SubmissionCodeDto? Atual);

public sealed record SubmissionEntryDto(Guid? SubmissionId, string Nome, int? VersaoAtual, DateTime? EnviadoEm);

public sealed record SubmissionDetailDto(Guid SubmissionId, string Nome, IReadOnlyList<SubmissionVersionDto> Versoes, SubmissionCodeDto? Atual);

public sealed record ReferenceSolutionDto(string? Linguagem, string? Codigo);
