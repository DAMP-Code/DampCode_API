using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace DampCode_API.Tests;

public sealed class HostingEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/Hackathons");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cors_WithoutConfiguration_AllowsViteDevServer()
    {
        var response = await factory.CreateClient().SendAsync(Preflight("http://localhost:5173"));

        Assert.Equal("http://localhost:5173", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Cors_WithConfiguredOrigins_AllowsOnlyThem()
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cors:Origins:0", "https://dampcode.exemplo.com");
        });
        var client = configured.CreateClient();

        var allowed = await client.SendAsync(Preflight("https://dampcode.exemplo.com"));
        var blocked = await client.SendAsync(Preflight("http://localhost:5173"));

        Assert.Equal("https://dampcode.exemplo.com", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(blocked.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
