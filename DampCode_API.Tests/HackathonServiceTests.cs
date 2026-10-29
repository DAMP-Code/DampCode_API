using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

namespace DampCode_API.Tests;

public sealed class HackathonServiceTests
{
    private readonly DampCodeDbContext _dbContext = TestDb.Create();
    private readonly HackathonService _service;

    public HackathonServiceTests() => _service = new HackathonService(_dbContext, new CompanyAccessService(_dbContext));

    private static CreateHackathonDto CompleteDto() => new()
    {
        Titulo = "Desafio de Rotas",
        Descricao = "Monte uma solução que reduza o tempo entre o pedido e a entrega.",
        Area = "web",
        Tecnologias = ["React", "Node"],
        Metodo = "Avaliação Manual",
        Ranking = "Top 10",
        Premiacao = 5000,
        Formato = "Equipe",
        TipoPremiacao = "Dinheiro",
        corPrincipal = "#6C48C5",
        corSecundaria = "#C68FE6",
        corFundo = "#170A36",
        DataInicio = DateTime.UtcNow.AddDays(2),
        DataFinal = DateTime.UtcNow.AddDays(5),
        DataFimAvaliacao = DateTime.UtcNow.AddDays(8)
    };

    private async Task<HackathonResponseDto> CreateAsync(Company company, CreateHackathonDto? dto = null)
    {
        var result = await _service.CreateAsync(company.PrimaryAccountId, dto ?? CompleteDto(), CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return result.Value!;
    }

    // Grava direto um Hackathon publicado e completo, com as datas que o teste precisa.
    private async Task<Hackathon> PublishedAsync(Company company, DateTime startsAt, string title = "Publicado")
    {
        var hackathon = new Hackathon
        {
            CompanyId = company.Id,
            CreatedByAccountId = company.PrimaryAccountId,
            Title = title,
            Slug = $"{title.ToLowerInvariant()}-{Guid.NewGuid():N}",
            Description = "Descrição do Hackathon publicado",
            Area = "web",
            ParticipationMode = HackathonParticipationMode.Individual,
            Status = HackathonStatus.Scheduled,
            EvaluationMethod = "Avaliação Manual",
            PrizeAmount = 1000,
            RewardType = HackathonRewardType.Money,
            WinnersCount = 10,
            StartsAt = startsAt,
            EndsAt = startsAt.AddDays(3),
            ReviewEndsAt = startsAt.AddDays(6),
            ScheduledAt = DateTime.UtcNow,
            PrimaryColor = "#6C48C5"
        };
        _dbContext.Hackathons.Add(hackathon);
        await _dbContext.SaveChangesAsync();
        return hackathon;
    }

    private Hackathon Stored(string id) => _dbContext.Hackathons.AsNoTracking()
        .Include(item => item.Technologies).ThenInclude(item => item.Technology)
        .Single(item => item.Id == Guid.Parse(id));

    [Fact]
    public async Task Hackathon_PersistsWithCompanyAndTechnologies()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var hackathon = new Hackathon
        {
            CompanyId = company.Id,
            CreatedByAccountId = company.PrimaryAccountId,
            Title = "Desafio de Rotas",
            Slug = "desafio-de-rotas",
            Description = "Descrição do desafio",
            Area = "web"
        };
        hackathon.Technologies.Add(new HackathonTechnology
        {
            Hackathon = hackathon,
            Technology = new Technology { Name = "React", NormalizedName = "REACT" }
        });
        _dbContext.Hackathons.Add(hackathon);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.Hackathons.Include(item => item.Company).Include(item => item.Technologies).SingleAsync();
        Assert.Equal("Empresa Exemplo", saved.Company.Name);
        Assert.Single(saved.Technologies);
        Assert.Equal(HackathonStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task Create_UnverifiedCompany_CreatesDraft()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var hackathon = await CreateAsync(company);

        Assert.Equal("Rascunho", hackathon.Fase);
        Assert.Equal("Empresa Exemplo", hackathon.Empresa);
        Assert.Equal("Top 10", hackathon.Ranking);
        Assert.Equal("Equipe", hackathon.Formato);
        Assert.Equal("Dinheiro", hackathon.TipoPremiacao);
        Assert.Equal(["Node", "React"], hackathon.Tecnologias);
        Assert.False(hackathon.Status);
        Assert.Equal(company.PrimaryAccountId, Stored(hackathon.HackathonId).CreatedByAccountId);
    }

    [Fact]
    public async Task Create_Employee_CreatesDraftForCompany()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = employee.Id });
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateAsync(employee.Id, CompleteDto(), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(company.Id, result.Value!.CompanyId);
        Assert.Equal(employee.Id, Stored(result.Value.HackathonId).CreatedByAccountId);
    }

    [Fact]
    public async Task Create_Participant_ReturnsForbidden()
    {
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");

        var result = await _service.CreateAsync(participant.Id, CompleteDto(), CancellationToken.None);

        Assert.Equal(HackathonServiceError.Forbidden, result.Error);
        Assert.Equal("Apenas empresas e seus funcionários podem gerenciar Hackathons.", result.Message);
    }

    [Fact]
    public async Task Create_MissingBasics_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var result = await _service.CreateAsync(company.PrimaryAccountId, new CreateHackathonDto { Titulo = "Só o título" }, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("Preencha: descrição, área, formato.", result.Message);
    }

    [Fact]
    public async Task Create_DuplicateTitle_GeneratesSuffixedSlug()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);

        var first = await CreateAsync(company);
        var second = await CreateAsync(company);

        Assert.Equal("desafio-de-rotas", Stored(first.HackathonId).Slug);
        Assert.Equal("desafio-de-rotas-2", Stored(second.HackathonId).Slug);
    }

    [Theory]
    [InlineData("Café & Código!", "cafe-codigo")]
    [InlineData("!!!", "hackathon")]
    public async Task Create_SlugWithoutAccents(string title, string expected)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.Titulo = title;

        var hackathon = await CreateAsync(company, dto);

        Assert.Equal(expected, Stored(hackathon.HackathonId).Slug);
    }

    [Fact]
    public async Task Create_DatesWithOffset_StoredAsUtc()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.DataInicio = new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.FromHours(-3)).UtcDateTime.ToLocalTime();
        dto.DataFinal = new DateTime(2030, 1, 12, 12, 0, 0, DateTimeKind.Unspecified);
        dto.DataFimAvaliacao = new DateTime(2030, 1, 14, 12, 0, 0, DateTimeKind.Utc);

        var stored = Stored((await CreateAsync(company, dto)).HackathonId);

        Assert.Equal(new DateTime(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc), stored.StartsAt);
        Assert.Equal(DateTimeKind.Utc, stored.StartsAt!.Value.Kind);
        Assert.Equal(new DateTime(2030, 1, 12, 12, 0, 0, DateTimeKind.Utc), stored.EndsAt);
        Assert.Equal(DateTimeKind.Utc, stored.EndsAt!.Value.Kind);
    }

    [Fact]
    public async Task Create_LogoOver2Mb_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.Logo = "data:image/png;base64," + new string('a', 2 * 1024 * 1024);

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("O logo deve ter no máximo 2 MB.", result.Message);
    }

    [Fact]
    public async Task Update_Draft_ReplacesAllFields()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var created = await CreateAsync(company);
        var dto = CompleteDto();
        dto.Titulo = "Novo título do desafio";
        dto.Premiacao = null;

        var result = await _service.UpdateAsync(company.PrimaryAccountId, Guid.Parse(created.HackathonId), dto, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var stored = Stored(created.HackathonId);
        Assert.Equal("Novo título do desafio", stored.Title);
        Assert.Null(stored.PrizeAmount);
        Assert.Equal("desafio-de-rotas", stored.Slug);
    }

    [Fact]
    public async Task Update_Draft_ReplacesTechnologies()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var created = await CreateAsync(company);
        var dto = CompleteDto();
        dto.Tecnologias = ["Node", "SQL"];

        var result = await _service.UpdateAsync(company.PrimaryAccountId, Guid.Parse(created.HackathonId), dto, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(["Node", "SQL"], Stored(created.HackathonId).Technologies.Select(item => item.Technology.Name).Order());
    }

    [Fact]
    public async Task Update_DraftDatesOutOfOrder_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var created = await CreateAsync(company);
        var dto = CompleteDto();
        dto.DataFinal = dto.DataInicio!.Value.AddHours(-1);

        var result = await _service.UpdateAsync(company.PrimaryAccountId, Guid.Parse(created.HackathonId), dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("As datas precisam seguir: início, fim das submissões, fim da avaliação.", result.Message);
    }

    [Fact]
    public async Task Update_Published_IgnoresCrucialFields()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var published = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        var dto = CompleteDto();
        dto.Titulo = "Título que não pode mudar";
        dto.Premiacao = 1;
        dto.corPrincipal = "#000000";

        var result = await _service.UpdateAsync(company.PrimaryAccountId, published.Id, dto, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var stored = Stored(published.Id.ToString());
        Assert.Equal("Publicado", stored.Title);
        Assert.Equal(1000, stored.PrizeAmount);
        Assert.Equal("#000000", stored.PrimaryColor);
    }

    [Fact]
    public async Task Update_Cancelled_ReturnsConflict()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        hackathon.Status = HackathonStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateAsync(company.PrimaryAccountId, hackathon.Id, CompleteDto(), CancellationToken.None);

        Assert.Equal(HackathonServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon já foi encerrado ou cancelado.", result.Message);
    }

    [Fact]
    public async Task Update_OtherCompany_ReturnsNotFound()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var other = await CompanySeed.CompanyAsync(_dbContext, "outra@exemplo.com");
        var created = await CreateAsync(company);

        var result = await _service.UpdateAsync(other.PrimaryAccountId, Guid.Parse(created.HackathonId), CompleteDto(), CancellationToken.None);

        Assert.Equal(HackathonServiceError.NotFound, result.Error);
        Assert.Equal("Hackathon não encontrado.", result.Message);
    }

    [Fact]
    public async Task Delete_Draft_RemovesIt()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var created = await CreateAsync(company);

        var result = await _service.DeleteAsync(company.PrimaryAccountId, Guid.Parse(created.HackathonId), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.False(_dbContext.Hackathons.Any());
    }

    [Fact]
    public async Task Delete_Published_ReturnsConflict()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));

        var result = await _service.DeleteAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Conflict, result.Error);
        Assert.Equal("Só é possível apagar rascunhos. Use cancelar.", result.Message);
    }
}
