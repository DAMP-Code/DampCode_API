namespace DampCode_API.Dto;

// Módulos e conteúdos do Hackathon, com os nomes de JSON que o front lê (seção 4 do design). As regras ficam no serviço.
public sealed class SaveHackathonModuleDto
{
    public string? Titulo { get; set; }
    public string? Descricao { get; set; }
}

// Tipo Texto usa só o texto; Link e Video usam a url, com o texto como descrição curta.
public sealed class SaveHackathonModuleContentDto
{
    public string? Tipo { get; set; }
    public string? Titulo { get; set; }
    public string? Texto { get; set; }
    public string? Url { get; set; }
}

// Lista completa de ids na nova ordem (módulos do Hackathon ou conteúdos de um módulo).
public sealed class HackathonModuleOrderDto
{
    public List<Guid>? Ids { get; set; }
}

public sealed record HackathonModuleContentDto(Guid ConteudoId, string Tipo, string Titulo, string? Texto, string? Url, int Posicao);

public sealed record HackathonModuleDto(Guid ModuloId, string Titulo, string? Descricao, int Posicao, IReadOnlyList<HackathonModuleContentDto> Conteudos);
