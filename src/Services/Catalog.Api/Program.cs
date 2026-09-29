using Cinema.Foundation;
using Cinema.Foundation.Storage;
using Catalog.Api.Endpoints;
using Catalog.Api.Repositories;
using Catalog.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<ICatalogQueryService, CatalogQueryService>();
builder.Services.AddScoped<ISeatMapService, SeatMapService>();
builder.Services.AddScoped<IBlockbusterCacheService, BlockbusterCacheService>();
builder.Services.AddCinemaStorage(builder.Configuration);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 120 * 1024 * 1024; // 120MB for trailers and media
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 120 * 1024 * 1024;
});


var app = builder.ConfigureCinemaService(
    "catalog",
    "Catalog & Showtimes Service — Manages cinema branches, auditoriums, movies, schedules, and visual seat layouts.");

app.MapFoundationEndpoints("catalog");
app.MapCatalogEndpoints();
app.MapAdminCatalogEndpoints();
app.MapAdminBranchEndpoints();
app.MapMediaEndpoints();

app.Run();

