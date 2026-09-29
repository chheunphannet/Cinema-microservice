using Cinema.Foundation;
using Identity.Api.Endpoints;
using Identity.Api.Repositories;
using Identity.Api.Security;
using Identity.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DI Registration
builder.Services.AddHttpClient();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<Identity.Api.Services.ICustomerAuthService, Identity.Api.Services.CustomerAuthService>();
builder.Services.AddScoped<ISupervisorService, SupervisorService>();
builder.Services.AddScoped<IAdminSystemRepository, AdminSystemRepository>();
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

// Override JWT Token Service to use RSA Key for generation
Identity.Api.Security.RsaKeyProvider.Initialize(builder.Configuration);

builder.Services.AddSingleton<Cinema.Foundation.Security.IJwtTokenService>(
    new Cinema.Foundation.Security.JwtTokenService(
        issuer: "cinemapos",
        audience: "cinemapos-clients",
        rsaKey: Identity.Api.Security.RsaKeyProvider.GetKey()));

// Use in-memory RSA key directly for Identity.Api's own JWT Bearer handler
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options =>
{
    var rsaKey = Identity.Api.Security.RsaKeyProvider.GetKey();
    options.TokenValidationParameters.IssuerSigningKey = rsaKey;
    var oidcConfig = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
    {
        Issuer = "cinemapos",
    };
    oidcConfig.SigningKeys.Add(rsaKey);
    options.Configuration = oidcConfig;
});

var app = builder.ConfigureCinemaService(
    "identity",
    "Identity & Access Management Service");

app.MapFoundationEndpoints("identity");
app.MapIdentityEndpoints();
app.MapAdminEndpoints();
app.MapAdminSystemEndpoints();
app.MapAdminDashboardEndpoints();
app.MapJwksEndpoints();

app.Run();

public partial class Program { }



