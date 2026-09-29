using Cinema.Foundation;
using Cinema.Foundation.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ticket.Api.Endpoints;
using Ticket.Api.Repositories;
using Ticket.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketIssuanceService, TicketIssuanceService>();
builder.Services.AddScoped<ITicketPrintService, TicketPrintService>();
builder.Services.AddScoped<ITicketRedemptionService, TicketRedemptionService>();
builder.Services.AddScoped<ITicketQueryService, TicketQueryService>();

builder.Services.AddCinemaEmail(builder.Configuration);
builder.Services.AddHostedService<TicketEventSubscriber>();

var app = builder.ConfigureCinemaService(
    "ticket",
    "Ticket Issuance & Gate Redemption Service — Manages ESC/POS thermal stub printing, QR vouchers, and duplicate-prevention state machine.");

app.MapFoundationEndpoints("ticket");
app.MapTicketEndpoints();

app.Run();
