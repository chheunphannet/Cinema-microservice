using Cinema.Foundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Reservation.Api.Endpoints;
using Reservation.Api.Repositories;
using Reservation.Api.Services;

using Cinema.Foundation.Email;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<ISeatHoldService, SeatHoldService>();
builder.Services.AddScoped<IReservationConfirmationService, ReservationConfirmationService>();
builder.Services.AddScoped<IReservationQueryService, ReservationQueryService>();
builder.Services.AddScoped<IAdminBookingRepository, AdminBookingRepository>();
builder.Services.AddScoped<IAdminBookingService, AdminBookingService>();
builder.Services.AddCinemaEmail(builder.Configuration);

builder.Services.AddHostedService<ReservationCleanupService>();

var app = builder.ConfigureCinemaService(
    "reservation", 
    "Reservation & Seat-Hold Service — Coordinates atomic seat holds with Redis Lua distributed locking, idempotency keys, and ACID PostgreSQL confirmation.");

app.MapFoundationEndpoints("reservation");
app.MapReservationEndpoints();
app.MapAdminBookingEndpoints();

app.Run();
