using System;
using System.Collections.Generic;
using System.Security.Claims;
using Cinema.Foundation;
using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Redis;
using Cinema.Foundation.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cinema.UnitTests;

public class FoundationServiceDefaultsAndCoverageTests
{
    [Fact]
    public void ServiceDefaults_ConfiguresCoreServicesAndEndpoints()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var builder = WebApplication.CreateBuilder(new string[] { });
        builder.Configuration["ConnectionStrings:WriteDb"] = "Host=localhost;Port=5432;Database=cinema;Username=cinema_app;Password=test";
        builder.Configuration["RabbitMQ:Password"] = "test-pass";
        builder.Configuration["Jwt:Secret"] = JwtTokenService.DefaultSecretKey;
        var app = builder.ConfigureCinemaService("test-service", "Unit test mock service description");

        Assert.NotNull(app);

        // Verify Core Building Blocks Registered in DI Container
        var sp = app.Services;
        Assert.NotNull(sp.GetService<IDbConnectionFactory>());
        Assert.NotNull(sp.GetService<ISeatLockService>());
        Assert.NotNull(sp.GetService<IEventBus>());
        Assert.NotNull(sp.GetService<IJwtTokenService>());
        
        var identity = sp.GetService<ServiceIdentity>();
        Assert.NotNull(identity);
        Assert.Equal("test-service", identity.Name);

        // Verify Foundation Endpoints Mapping
        var configuredApp = app.MapFoundationEndpoints("test-service");
        Assert.NotNull(configuredApp);
    }

    [Fact]
    public void DbConnectionFactory_ShouldHandleVariousConnectionStringConfigurations()
    {
        // 1. Missing write connection string throws ArgumentException (Hardened security)
        Assert.Throws<ArgumentException>(() => new NpgsqlConnectionFactory(null, null));

        // 2. Custom write string only (read defaults to write)
        const string customWrite = "Host=primary.db;Port=5432;Database=cinema;Username=app;Password=pass";
        var writeOnlyFactory = new NpgsqlConnectionFactory(customWrite, null);
        using var w1 = writeOnlyFactory.CreateConnection();
        using var r1 = writeOnlyFactory.CreateReadConnection();
        Assert.Contains("primary.db", w1.ConnectionString);
        Assert.Equal(w1.ConnectionString, r1.ConnectionString);

        // 3. Custom write and replica read string
        const string customRead = "Host=replica.db;Port=5432;Database=cinema;Username=app;Password=pass";
        var dualFactory = new NpgsqlConnectionFactory(customWrite, customRead);
        using var w2 = dualFactory.CreateConnection();
        using var r2 = dualFactory.CreateReadConnection();
        Assert.Contains("primary.db", w2.ConnectionString);
        Assert.Contains("replica.db", r2.ConnectionString);
        Assert.NotEqual(w2.ConnectionString, r2.ConnectionString);
    }

    [Fact]
    public void JwtTokenService_HandlesNullOptionalClaimsAndCustomKeys()
    {
        const string customSecret = "ThisIsACustom32ByteSecretKeyForTesting123!";
        const string customIssuer = "CustomCinemaIssuer";
        const string customAudience = "CustomCinemaAudience";

        var service = new JwtTokenService(customSecret, customIssuer, customAudience);
        var userId = Guid.NewGuid();

        // Generate token with valid role and null branch
        var token = service.GenerateToken(userId, "test_admin", "Test Administrator", "system_admin", null);
        Assert.NotNull(token);

        var principal = service.ValidateToken(token);
        Assert.NotNull(principal);

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(userId.ToString(), idClaim);

        // Tampered token fails validation
        var invalidToken = token.Substring(0, token.Length - 5) + "abcde";
        Assert.Null(service.ValidateToken(invalidToken));

        // Empty token fails validation
        Assert.Null(service.ValidateToken(""));
        Assert.Null(service.ValidateToken("not.a.valid.jwt.token"));
    }

    [Fact]
    public void PasswordHasher_EdgeCases()
    {
        Assert.False(PasswordHasher.Verify("1234", ""));
        Assert.False(PasswordHasher.Verify("", "somehash"));
        Assert.False(PasswordHasher.Verify("1234", "tooshort"));

        var hash = PasswordHasher.Hash("secret-password");
        Assert.True(PasswordHasher.Verify("secret-password", hash));
        Assert.True(PasswordHasher.Verify("secret-password", hash.ToLowerInvariant()));
        Assert.True(PasswordHasher.Verify("secret-password", hash.ToUpperInvariant()));
    }

    [Fact]
    public async Task RabbitMqEventBus_HandlesConnectionFailuresGracefully()
    {
        var bus = new RabbitMqEventBus(hostName: "127.0.0.1", port: 59999, password: "test-pass");
        
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync("ticket.redeemed", new { Data = "test" }));
        Assert.Contains("RabbitMQ unavailable", ex.Message);
        
        var subEx = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SubscribeAsync<object>("test_queue", "test.event", async (e) => await Task.CompletedTask));
        Assert.Contains("RabbitMQ unavailable", subEx.Message);

        await bus.DisposeAsync();
    }

    [Fact]
    public void ServiceIdentity_StoresServiceMetadataAccurately()
    {
        var identity = new ServiceIdentity(
            Name: "catalog",
            WriteDatabase: "Host=postgres;Port=5432;Database=cinema",
            ReadDatabase: "Host=postgres-replica;Port=5432;Database=cinema",
            Redis: "redis:6379");

        Assert.Equal("catalog", identity.Name);
        Assert.Equal("Host=postgres;Port=5432;Database=cinema", identity.WriteDatabase);
        Assert.Equal("Host=postgres-replica;Port=5432;Database=cinema", identity.ReadDatabase);
        Assert.Equal("redis:6379", identity.Redis);
    }

    [Fact]
    public void IntegrationEvents_SerializeAndDeserializePreservingAllFields()
    {
        var resId = Guid.NewGuid();
        var showtimeId = Guid.NewGuid();
        var seats = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var now = DateTimeOffset.UtcNow;

        var resEvent = new ReservationConfirmedIntegrationEvent(resId, showtimeId, seats, 13.00m, now);
        var resJson = System.Text.Json.JsonSerializer.Serialize(resEvent);
        var deserializedRes = System.Text.Json.JsonSerializer.Deserialize<ReservationConfirmedIntegrationEvent>(resJson);

        Assert.NotNull(deserializedRes);
        Assert.Equal(resId, deserializedRes.ReservationId);
        Assert.Equal(showtimeId, deserializedRes.ShowtimeId);
        Assert.Equal(2, deserializedRes.SeatIds.Count);
        Assert.Equal(13.00m, deserializedRes.TotalAmount);

        var payEvent = new PaymentCapturedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "cash", 20.00m, now);
        var payJson = System.Text.Json.JsonSerializer.Serialize(payEvent);
        var deserializedPay = System.Text.Json.JsonSerializer.Deserialize<PaymentCapturedIntegrationEvent>(payJson);
        Assert.NotNull(deserializedPay);
        Assert.Equal("cash", deserializedPay.Method);
        Assert.Equal(20.00m, deserializedPay.Amount);

        var ticketEvent = new TicketRedeemedIntegrationEvent(Guid.NewGuid(), "GATE-1", now);
        var ticketJson = System.Text.Json.JsonSerializer.Serialize(ticketEvent);
        var deserializedTicket = System.Text.Json.JsonSerializer.Deserialize<TicketRedeemedIntegrationEvent>(ticketJson);
        Assert.NotNull(deserializedTicket);
        Assert.Equal("GATE-1", deserializedTicket.TerminalCode);
    }
}
