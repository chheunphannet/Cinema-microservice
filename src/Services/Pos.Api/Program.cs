using Cinema.Foundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.Endpoints;
using Pos.Api.Repositories;
using Pos.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddScoped<IPosRepository, PosRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IWebhookIdempotencyService, WebhookIdempotencyService>();
builder.Services.AddScoped<IComboEngineService, ComboEngineService>();
builder.Services.AddHttpClient("LoyaltyClient").AddStandardResilienceHandler();

var app = builder.ConfigureCinemaService(
    "pos",
    "Point of Sale (POS) Service — Handles F&B orders, till shifts, local payments, and idempotent external webhooks.");

app.MapFoundationEndpoints("pos");
app.MapPosEndpoints();
app.MapAdminInventoryEndpoints();
app.MapAdminMenuEndpoints();
app.MapAdminFinanceEndpoints();

app.Run();

