using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DampCode_API.Tests;

public sealed class CompaniesEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"{Guid.NewGuid():N}@exemplo.com";

    private static async Task<AuthResponseDto> RegisterParticipantAsync(HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/participante", new
        {
            name = "Maria Lima",
            email = email ?? NewEmail(),
            password = "SenhaForte123"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static async Task<AuthResponseDto> RegisterCompanyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/register/empresa", new
        {
            name = "Empresa Exemplo",
            email = NewEmail(),
            password = "SenhaForte123",
            cnpj = string.Concat(Enumerable.Range(0, 14).Select(_ => Random.Shared.Next(10))),
            area = "Tecnologia",
            descricao = "Empresa de software de exemplo"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static async Task<AuthResponseDto> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = "SenhaForte123" });
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task UpdateDatabaseAsync(Action<DampCodeDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DampCodeDbContext>();
        change(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<CompanyInvitationDto> InviteAsync(HttpClient client, string companyToken, string email)
    {
        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Companies/me/invitations", companyToken,
            new { email, jobTitle = "Desenvolvedora" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CompanyInvitationDto>())!;
    }

    private async Task<(AuthResponseDto Company, AuthResponseDto Employee)> CompanyWithEmployeeAsync(HttpClient client)
    {
        var company = await RegisterCompanyAsync(client);
        var employee = await RegisterParticipantAsync(client);
        var invitation = await InviteAsync(client, company.Token, employee.Email);
        var accept = await client.SendAsync(Request(HttpMethod.Post, $"/api/Companies/invitations/{invitation.Id}/accept", employee.Token));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        return (company, employee);
    }

    [Fact]
    public async Task Companies_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Companies/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ParticipantWithoutCompany_Returns404()
    {
        var client = factory.CreateClient();
        var participant = await RegisterParticipantAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/me", participant.Token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InviteEmployee_ByParticipant_Returns403()
    {
        var client = factory.CreateClient();
        var participant = await RegisterParticipantAsync(client);
        var other = await RegisterParticipantAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Companies/me/invitations", participant.Token, new { email = other.Email }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InviteEmployee_ByEmployee_Returns403()
    {
        var client = factory.CreateClient();
        var (_, employee) = await CompanyWithEmployeeAsync(client);
        var other = await RegisterParticipantAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/Companies/me/invitations", employee.Token, new { email = other.Email }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveEmployee_ByEmployee_Returns403()
    {
        var client = factory.CreateClient();
        var (_, employee) = await CompanyWithEmployeeAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Delete, $"/api/Companies/me/employees/{employee.Id}", employee.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PendingVerification_ByCompany_Returns403()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);

        var response = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/pending-verification", company.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PendingVerification_ByAdmin_Returns200()
    {
        var client = factory.CreateClient();
        var registered = await RegisterParticipantAsync(client);
        await UpdateDatabaseAsync(db => db.Accounts.Single(account => account.Id == registered.Id).AccountType = AccountType.Admin);
        var admin = await LoginAsync(client, registered.Email);

        var response = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/pending-verification", admin.Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InviteAcceptFlow_EmployeeAppearsInList()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var employee = await RegisterParticipantAsync(client);
        var invitation = await InviteAsync(client, company.Token, employee.Email);

        var received = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/invitations/received", employee.Token));
        var receivedBody = await received.Content.ReadFromJsonAsync<List<ReceivedInvitationDto>>();
        Assert.Equal(invitation.Id, Assert.Single(receivedBody!).Id);

        var accept = await client.SendAsync(Request(HttpMethod.Post, $"/api/Companies/invitations/{invitation.Id}/accept", employee.Token));
        var access = await accept.Content.ReadFromJsonAsync<CompanyAccessDto>();
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.False(access!.IsPrimaryAccount);

        var employees = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/me/employees", company.Token));
        var employeesBody = await employees.Content.ReadFromJsonAsync<List<CompanyEmployeeDto>>();
        Assert.Contains(employeesBody!, item => item.Email == employee.Email);

        var me = await client.SendAsync(Request(HttpMethod.Get, "/api/Companies/me", employee.Token));
        var meBody = await me.Content.ReadFromJsonAsync<CompanyAccessDto>();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(access.CompanyId, meBody!.CompanyId);
    }

    [Fact]
    public async Task AcceptInvitation_Expired_Returns410()
    {
        var client = factory.CreateClient();
        var company = await RegisterCompanyAsync(client);
        var employee = await RegisterParticipantAsync(client);
        var invitation = await InviteAsync(client, company.Token, employee.Email);
        await UpdateDatabaseAsync(db => db.CompanyInvitations.Single(item => item.Id == invitation.Id).ExpiresAt = DateTime.UtcNow.AddMinutes(-1));

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/api/Companies/invitations/{invitation.Id}/accept", employee.Token));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}
