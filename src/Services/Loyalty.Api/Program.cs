using Cinema.Foundation;
using Loyalty.Api.Endpoints;
using Loyalty.Api.Repositories;
using Loyalty.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IAdminLoyaltyRepository, AdminLoyaltyRepository>();
builder.Services.AddScoped<IAdminLoyaltyService, AdminLoyaltyService>();

builder.Services.AddHostedService<Loyalty.Api.Workers.RabbitMqLoyaltyConsumer>();

var app = builder.ConfigureCinemaService(
    "loyalty",
    "Loyalty & CRM Service");

app.MapFoundationEndpoints("loyalty");
app.MapLoyaltyEndpoints();
app.MapAdminLoyaltyEndpoints();

app.Run();

public partial class Program { }
