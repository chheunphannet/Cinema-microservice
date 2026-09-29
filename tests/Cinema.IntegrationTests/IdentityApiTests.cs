using System;
using System.Net.Http.Json;
using Identity.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Cinema.IntegrationTests;

public class IdentityApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    static IdentityApiTests()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__WriteDb", "Host=localhost;Port=5432;Database=cinema;Username=cinema_app;Password=test");
        Environment.SetEnvironmentVariable("ConnectionStrings__ReadDb", "Host=localhost;Port=5432;Database=cinema;Username=cinema_app;Password=test");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "test-pass");
        Environment.SetEnvironmentVariable("Jwt__Secret", "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes");
    }

    public IdentityApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealthContract_ReturnsSuccessAndAnonymous()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/identity/health-contract");
        
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("schema", content);
    }

    [Fact]
    public async Task GoogleLogin_ReturnsUnauthorized_WhenIdTokenInvalid()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/identity/customers/google-login", new GoogleLoginRequest("invalid-random-token"));
        
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_ReturnsBadRequest_WhenIdTokenEmpty()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/identity/customers/google-login", new GoogleLoginRequest(""));
        
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
