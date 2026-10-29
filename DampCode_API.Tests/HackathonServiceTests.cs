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

    [Theory]
    [InlineData("título", 200)]
    [InlineData("descrição", 4000)]
    [InlineData("área", 100)]
    [InlineData("método de avaliação", 100)]
    [InlineData("tecnologias", 100)]
    public async Task Create_TextOverColumnLimit_ReturnsValidation(string field, int limit)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        var text = new string('a', limit + 1);
        switch (field)
        {
            case "título": dto.Titulo = text; break;
            case "descrição": dto.Descricao = text; break;
            case "área": dto.Area = text; break;
            case "método de avaliação": dto.Metodo = text; break;
            default: dto.Tecnologias = ["React", text]; break;
        }

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal($"O campo {field} deve ter no máximo {limit} caracteres.", result.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_000_000_000)]
    public async Task Create_PrizeOutOfRange_ReturnsValidation(decimal prize)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.Premiacao = prize;

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("A premiação deve ficar entre 0 e 9.999.999.999,99.", result.Message);
    }

    [Fact]
    public async Task Create_InterviewReward_DropsPrizeAmount()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.TipoPremiacao = "Entrevista";
        dto.Premiacao = 5000;

        var hackathon = await CreateAsync(company, dto);

        Assert.Null(Stored(hackathon.HackathonId).PrizeAmount);
        Assert.Equal(0, hackathon.Premiacao);
    }

    [Fact]
    public async Task Create_InvalidColor_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.corFundo = "red;background:url(x)";

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("As cores devem estar no formato #RRGGBB.", result.Message);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    public async Task Create_LogoNotRasterImage_ReturnsValidation(string logo)
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.Logo = logo;

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("O logo deve ser uma imagem PNG, JPEG, WEBP ou GIF.", result.Message);
    }

    [Fact]
    public async Task Update_PublishedInvalidColor_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var published = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        var dto = CompleteDto();
        dto.corPrincipal = "#12345";

        var result = await _service.UpdateAsync(company.PrimaryAccountId, published.Id, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("#6C48C5", Stored(published.Id.ToString()).PrimaryColor);
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

    private async Task<Company> VerifiedCompanyAsync(string email = "empresa@exemplo.com") =>
        await CompanySeed.CompanyAsync(_dbContext, email, CompanyVerificationStatus.Verified);

    private Task<HackathonServiceResult<HackathonResponseDto>> PublishAsync(Company company, string id) =>
        _service.PublishAsync(company.PrimaryAccountId, Guid.Parse(id), CancellationToken.None);

    [Fact]
    public async Task Publish_VerifiedComplete_SchedulesIt()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company);

        var result = await PublishAsync(company, created.HackathonId);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Publicado", result.Value!.Fase);
        Assert.True(result.Value.Status);
        var stored = Stored(created.HackathonId);
        Assert.Equal(HackathonStatus.Scheduled, stored.Status);
        Assert.NotNull(stored.ScheduledAt);
    }

    [Fact]
    public async Task Publish_UnverifiedCompany_ReturnsForbidden()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var created = await CreateAsync(company);

        var result = await PublishAsync(company, created.HackathonId);

        Assert.Equal(HackathonServiceError.Forbidden, result.Error);
        Assert.Equal("Sua empresa precisa ser verificada para publicar.", result.Message);
    }

    [Fact]
    public async Task Publish_MissingFields_ListsThem()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company, new CreateHackathonDto
        {
            Titulo = "Só o básico",
            Descricao = "Descrição do rascunho incompleto",
            Area = "web",
            Formato = "Individual"
        });

        var result = await PublishAsync(company, created.HackathonId);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("Preencha antes de publicar: tecnologias, data de início, fim das submissões, fim da avaliação, "
            + "tipo de premiação, quantos ganham, método de avaliação.", result.Message);
    }

    [Fact]
    public async Task Publish_StartInPast_ReturnsValidation()
    {
        var company = await VerifiedCompanyAsync();
        var dto = CompleteDto();
        dto.DataInicio = DateTime.UtcNow.AddMinutes(-1);
        var created = await CreateAsync(company, dto);

        var result = await PublishAsync(company, created.HackathonId);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("A data de início precisa ser no futuro.", result.Message);
    }

    [Fact]
    public async Task Publish_DatesOutOfOrder_ReturnsValidation()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company);
        var stored = _dbContext.Hackathons.Single();
        stored.ReviewEndsAt = stored.EndsAt!.Value.AddHours(-1);
        await _dbContext.SaveChangesAsync();

        var result = await PublishAsync(company, created.HackathonId);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("As datas precisam seguir: início, fim das submissões, fim da avaliação.", result.Message);
    }

    [Fact]
    public async Task Publish_InterviewWithoutPrize_Succeeds()
    {
        var company = await VerifiedCompanyAsync();
        var dto = CompleteDto();
        dto.TipoPremiacao = "Entrevista";
        dto.Premiacao = null;
        var created = await CreateAsync(company, dto);

        var result = await PublishAsync(company, created.HackathonId);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Entrevista", result.Value!.TipoPremiacao);
    }

    [Fact]
    public async Task Publish_MoneyWithoutPrize_ListsPrize()
    {
        var company = await VerifiedCompanyAsync();
        var dto = CompleteDto();
        dto.Premiacao = 0;
        var created = await CreateAsync(company, dto);

        var result = await PublishAsync(company, created.HackathonId);

        Assert.Equal("Preencha antes de publicar: premiação.", result.Message);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_ReturnsConflict()
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));

        var result = await PublishAsync(company, hackathon.Id.ToString());

        Assert.Equal(HackathonServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon já foi publicado.", result.Message);
    }

    [Fact]
    public async Task Publish_ByEmployee_Succeeds()
    {
        var company = await VerifiedCompanyAsync();
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = employee.Id });
        await _dbContext.SaveChangesAsync();
        var created = await CreateAsync(company);

        var result = await _service.PublishAsync(employee.Id, Guid.Parse(created.HackathonId), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task Update_RemovedEmployee_ReturnsForbidden()
    {
        var company = await VerifiedCompanyAsync();
        var employee = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");
        _dbContext.CompanyMemberships.Add(new CompanyMembership { CompanyId = company.Id, AccountId = employee.Id, EndedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var created = await CreateAsync(company);

        var result = await _service.UpdateAsync(employee.Id, Guid.Parse(created.HackathonId), CompleteDto(), CancellationToken.None);

        Assert.Equal(HackathonServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Cancel_Published_CancelsIt()
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));

        var result = await _service.CancelAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Cancelado", result.Value!.Fase);
        Assert.NotNull(Stored(hackathon.Id.ToString()).CancelledAt);
    }

    [Fact]
    public async Task Cancel_Cancelled_ReturnsConflict()
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        await _service.CancelAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        var result = await _service.CancelAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Conflict, result.Error);
        Assert.Equal("Este Hackathon já foi encerrado ou cancelado.", result.Message);
    }

    [Fact]
    public async Task Close_OutsideReview_ReturnsConflict()
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));

        var result = await _service.CloseAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Conflict, result.Error);
        Assert.Equal("Só é possível encerrar durante a avaliação.", result.Message);
    }

    [Fact]
    public async Task Close_InReview_ClosesIt()
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(-4));

        var result = await _service.CloseAsync(company.PrimaryAccountId, hackathon.Id, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Encerrado", result.Value!.Fase);
        var stored = Stored(hackathon.Id.ToString());
        Assert.Equal(HackathonStatus.Closed, stored.Status);
        Assert.NotNull(stored.ClosedAt);
    }

    [Fact]
    public async Task GetPublic_HidesDraftAndCancelled_ClosedLast()
    {
        var company = await VerifiedCompanyAsync();
        await CreateAsync(company);
        var cancelled = await PublishedAsync(company, DateTime.UtcNow.AddDays(1), "Cancelado");
        await _service.CancelAsync(company.PrimaryAccountId, cancelled.Id, CancellationToken.None);
        await PublishedAsync(company, DateTime.UtcNow.AddDays(-10), "Encerrado");
        await PublishedAsync(company, DateTime.UtcNow.AddDays(5), "Depois");
        await PublishedAsync(company, DateTime.UtcNow.AddDays(2), "Antes");

        var hackathons = await _service.GetPublicAsync(CancellationToken.None);

        Assert.Equal(["Antes", "Depois", "Encerrado"], hackathons.Select(item => item.Titulo));
        Assert.Equal("Encerrado", hackathons[2].Fase);
        Assert.False(hackathons[2].Status);
    }

    [Fact]
    public async Task GetCompanyHackathons_IncludesDrafts()
    {
        var company = await VerifiedCompanyAsync();
        var other = await VerifiedCompanyAsync("outra@exemplo.com");
        await CreateAsync(company);
        await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        await PublishedAsync(other, DateTime.UtcNow.AddDays(2), "Outra");

        var result = await _service.GetCompanyHackathonsAsync(company.PrimaryAccountId, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, item => item.Fase == "Rascunho");
    }

    [Fact]
    public async Task GetCompanyHackathons_Participant_ReturnsForbidden()
    {
        var participant = await CompanySeed.ParticipantAsync(_dbContext, "maria@exemplo.com");

        var result = await _service.GetCompanyHackathonsAsync(participant.Id, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetById_DraftForAnonymous_ReturnsNotFound()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company);

        var result = await _service.GetByIdAsync(null, created.HackathonId, CancellationToken.None);

        Assert.Equal(HackathonServiceError.NotFound, result.Error);
        Assert.Equal("Hackathon não encontrado.", result.Message);
    }

    [Fact]
    public async Task GetById_DraftForManager_ReturnsIt()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company);

        var result = await _service.GetByIdAsync(company.PrimaryAccountId, created.HackathonId, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("Rascunho", result.Value!.Fase);
    }

    [Fact]
    public async Task GetById_BySlug_ReturnsPublished()
    {
        var company = await VerifiedCompanyAsync();
        var created = await CreateAsync(company);
        await PublishAsync(company, created.HackathonId);

        var result = await _service.GetByIdAsync(null, "desafio-de-rotas", CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(created.HackathonId, result.Value!.HackathonId);
    }

    [Fact]
    public async Task Update_Published_SavesReference()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var published = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        var dto = CompleteDto();
        dto.Titulo = "Título que não pode mudar";
        dto.LinguagemReferencia = "Python";
        dto.SolucaoReferencia = "def resposta(): pass";

        var result = await _service.UpdateAsync(company.PrimaryAccountId, published.Id, dto, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var stored = Stored(published.Id.ToString());
        Assert.Equal("Publicado", stored.Title);
        Assert.Equal("Python", stored.ReferenceLanguage);
        Assert.Equal("def resposta(): pass", stored.ReferenceCode);
    }

    [Fact]
    public async Task Create_ReferenceOver100Kb_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.LinguagemReferencia = "Python";
        dto.SolucaoReferencia = new string('a', 102401);

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal(HackathonServiceError.Validation, result.Error);
        Assert.Equal("O código deve ter no máximo 100 KB.", result.Message);
    }

    [Fact]
    public async Task Create_ReferenceWithoutLanguage_ReturnsValidation()
    {
        var company = await CompanySeed.CompanyAsync(_dbContext);
        var dto = CompleteDto();
        dto.SolucaoReferencia = "def resposta(): pass";

        var result = await _service.CreateAsync(company.PrimaryAccountId, dto, CancellationToken.None);

        Assert.Equal("Escolha uma linguagem.", result.Message);
    }

    private async Task AddParticipantsAsync(Hackathon hackathon, int active, int left = 0)
    {
        for (var index = 0; index < active + left; index++)
        {
            var account = await CompanySeed.ParticipantAsync(_dbContext, $"p{index}-{Guid.NewGuid():N}@exemplo.com");
            _dbContext.Participations.Add(new Participation
            {
                HackathonId = hackathon.Id,
                AccountId = account.Id,
                LeftAt = index < active ? null : DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    [Theory]
    [InlineData(0, 0, "0 pessoas já entraram")]
    [InlineData(1, 0, "1 pessoa já entrou")]
    [InlineData(2, 1, "2 pessoas já entraram")]
    public async Task Participants_CountsActiveOnly(int active, int left, string expected)
    {
        var company = await VerifiedCompanyAsync();
        var hackathon = await PublishedAsync(company, DateTime.UtcNow.AddDays(2));
        await AddParticipantsAsync(hackathon, active, left);

        var byId = await _service.GetByIdAsync(null, hackathon.Id.ToString(), CancellationToken.None);
        var listed = (await _service.GetPublicAsync(CancellationToken.None)).Single();

        Assert.Equal(expected, byId.Value!.Participantes);
        Assert.Equal(expected, listed.Participantes);
    }
}
