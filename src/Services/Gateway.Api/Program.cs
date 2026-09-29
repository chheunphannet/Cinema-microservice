using Cinema.Foundation;
using Gateway.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog with Seq sink
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "gateway-api")
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:8080", "http://localhost:4321" };
    policy.WithOrigins(allowedOrigins)
          .WithHeaders("Authorization", "Content-Type", "X-Idempotency-Key", "X-Branch-Id", "X-Terminal-Id")
          .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS");
}));

builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    // In production, add known proxies/networks here. E.g.:
    // options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
          var trustedProxy = builder.Configuration["Gateway:TrustedProxy"];
      if (!string.IsNullOrEmpty(trustedProxy))
      {
          options.KnownProxies.Add(System.Net.IPAddress.Parse(trustedProxy));
      }
      else
      {
          options.KnownNetworks.Clear();
          options.KnownProxies.Clear();
      }
});

static string GetClientIp(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// API Gateway Rate Limiting (DDoS Protection / WAF-lite)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // 100 requests per IP
                QueueLimit = 2,
                Window = TimeSpan.FromSeconds(10) // Every 10 seconds
            }));

    options.AddPolicy("AntiHoardPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("CheckoutPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("ETicketPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("StrictAuthPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("MediaServingPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60,
                QueueLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("MediaUploadPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(context),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 20,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 120 * 1024 * 1024; // 120MB for trailers and media
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 120 * 1024 * 1024;
});



// Add Token Revocation dependencies
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? "redis:6379";
});
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
    StackExchange.Redis.ConnectionMultiplexer.Connect(builder.Configuration["Redis:Configuration"] ?? "redis:6379,abortConnect=false"));
builder.Services.AddSingleton<Cinema.Foundation.Security.ITokenRevocationService, Cinema.Foundation.Security.TokenRevocationService>();

// Add JWT Authentication to Gateway (Module 3 Security)
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = "http://identity-api:8080/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "cinemapos",
            ValidateAudience = true,
            ValidAudience = "cinemapos-clients",
            ValidateLifetime = true
        };

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<Cinema.Foundation.Security.ITokenRevocationService>();
                    var isRevoked = await tokenRevocationService.IsRevokedAsync(jti);
                    if (isRevoked)
                    {
                        context.Fail("This token has been revoked.");
                    }
                }
            }
        };
    });
builder.Services.AddAuthorization();

// OpenTelemetry Metrics & Prometheus Exporter (Module 3.2 Operability & Module 5.1 Performance Gates)
var gatewayOtelServiceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "gateway-api";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: gatewayOtelServiceName,
            serviceNamespace: "CinemaPos",
            serviceVersion: "1.0.0")
        .AddTelemetrySdk()
        .AddEnvironmentVariableDetector())
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddMeter("System.Net.Http")
            .AddPrometheusExporter()
            .AddOtlpExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

var catalogUrl = builder.Configuration["Services:CatalogUrl"] ?? "http://catalog-api:8080";
var reservationUrl = builder.Configuration["Services:ReservationUrl"] ?? "http://reservation-api:8080";
var posUrl = builder.Configuration["Services:PosUrl"] ?? "http://pos-api:8080";
var ticketUrl = builder.Configuration["Services:TicketUrl"] ?? "http://ticket-api:8080";
var identityUrl = builder.Configuration["Services:IdentityUrl"] ?? "http://identity-api:8080";
var loyaltyUrl = builder.Configuration["Services:LoyaltyUrl"] ?? "http://loyalty-api:8080";
var minioConsoleUrl = builder.Configuration["Services:MinioConsoleUrl"] ?? "http://minio:9001";
var minioS3Url = builder.Configuration["Services:MinioS3Url"] ?? "http://minio:9000";

var routes = GatewayProxyConfig.GetRoutes(builder.Configuration);
var clusters = GatewayProxyConfig.GetClusters(builder.Configuration);

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.Equals("/minio-console", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Response.Redirect("/minio-console/", permanent: true);
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

// Operability & Telemetry Middleware (Module 3.2 & Module 5.1): Correlation ID & Response Latency
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("D");
    }
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    context.Response.OnStarting(() =>
    {
        sw.Stop();
        context.Response.Headers["X-Response-Time-Ms"] = sw.ElapsedMilliseconds.ToString();
        return Task.CompletedTask;
    });

    await next();
});

// Prometheus Metrics Scraper Endpoint (Module 3.2 & Module 5.1)
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/proxy-swagger/all", "⭐ ALL Microservices (Combined Unified API)");
    c.SwaggerEndpoint("/proxy-swagger/pos", "3. POS & Box-Office Operations API (Webhooks & Orders)");
    c.SwaggerEndpoint("/proxy-swagger/catalog", "1. Catalog & Showtimes API");
    c.SwaggerEndpoint("/proxy-swagger/reservation", "2. Reservation & Seat Locking API");
    c.SwaggerEndpoint("/proxy-swagger/ticket", "4. Ticket Issuance & Gate Redemption API");
    c.SwaggerEndpoint("/proxy-swagger/identity", "5. Identity & Access Management API");
    c.SwaggerEndpoint("/proxy-swagger/loyalty", "6. Loyalty & Rewards API");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Cinema POS — Unified API Gateway Documentation";
});

app.MapGet("/proxy-swagger/{service}", async (string service, IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient();

    if (service.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        var serviceUrls = new[]
        {
            $"{posUrl}/swagger/v1/swagger.json",
            $"{catalogUrl}/swagger/v1/swagger.json",
            $"{reservationUrl}/swagger/v1/swagger.json",
            $"{ticketUrl}/swagger/v1/swagger.json",
            $"{identityUrl}/swagger/v1/swagger.json",
            $"{loyaltyUrl}/swagger/v1/swagger.json"
        };

        var rootNode = new System.Text.Json.Nodes.JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new System.Text.Json.Nodes.JsonObject
            {
                ["title"] = "Cinema POS — Complete Unified Microservices Fleet",
                ["version"] = "v1",
                ["description"] = "Aggregated OpenAPI specification combining all 5 microservices (POS, Catalog, Reservation, Ticket, and Identity)."
            },
            ["security"] = new System.Text.Json.Nodes.JsonArray
            {
                new System.Text.Json.Nodes.JsonObject
                {
                    ["Bearer"] = new System.Text.Json.Nodes.JsonArray()
                }
            },
            ["paths"] = new System.Text.Json.Nodes.JsonObject(),
            ["components"] = new System.Text.Json.Nodes.JsonObject
            {
                ["schemas"] = new System.Text.Json.Nodes.JsonObject(),
                ["securitySchemes"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["Bearer"] = new System.Text.Json.Nodes.JsonObject
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer",
                        ["bearerFormat"] = "JWT",
                        ["description"] = "Enter JWT Bearer token"
                    }
                }
            }
        };

        var paths = rootNode["paths"]!.AsObject();
        var schemas = rootNode["components"]!["schemas"]!.AsObject();

        foreach (var url in serviceUrls)
        {
            try
            {
                var jsonStr = await client.GetStringAsync(url);
                var doc = System.Text.Json.Nodes.JsonNode.Parse(jsonStr)?.AsObject();
                if (doc != null)
                {
                    if (doc["paths"] is System.Text.Json.Nodes.JsonObject docPaths)
                    {
                        foreach (var prop in docPaths)
                        {
                            if (!paths.ContainsKey(prop.Key) && prop.Value != null)
                            {
                                var pathItem = prop.Value.DeepClone();
                                if (pathItem is System.Text.Json.Nodes.JsonObject pathObj)
                                {
                                    foreach (var op in pathObj)
                                    {
                                        if (op.Value is System.Text.Json.Nodes.JsonObject opObj)
                                        {
                                            if (prop.Key.StartsWith("/api/v1/identity/login") || prop.Key.StartsWith("/api/v1/pos/payments/webhook") || prop.Key.StartsWith("/.well-known") || prop.Key.StartsWith("/api/v1/identity/customers"))
                                            {
                                                opObj["security"] = new System.Text.Json.Nodes.JsonArray();
                                            }
                                            else
                                            {
                                                opObj["security"] = new System.Text.Json.Nodes.JsonArray
                                                {
                                                    new System.Text.Json.Nodes.JsonObject
                                                    {
                                                        ["Bearer"] = new System.Text.Json.Nodes.JsonArray()
                                                    }
                                                };
                                            }
                                        }
                                    }
                                }
                                paths[prop.Key] = pathItem;
                            }
                        }
                    }
                    if (doc["components"]?["schemas"] is System.Text.Json.Nodes.JsonObject docSchemas)
                    {
                        foreach (var prop in docSchemas)
                        {
                            if (!schemas.ContainsKey(prop.Key) && prop.Value != null)
                            {
                                schemas[prop.Key] = prop.Value.DeepClone();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return Results.Content(rootNode.ToJsonString(), "application/json");
    }

    var targetUrl = service.ToLowerInvariant() switch
    {
        "catalog" => $"{catalogUrl}/swagger/v1/swagger.json",
        "reservation" => $"{reservationUrl}/swagger/v1/swagger.json",
        "pos" => $"{posUrl}/swagger/v1/swagger.json",
        "ticket" => $"{ticketUrl}/swagger/v1/swagger.json",
        "identity" => $"{identityUrl}/swagger/v1/swagger.json",
        "loyalty" => $"{loyaltyUrl}/swagger/v1/swagger.json",
        _ => null
    };

    if (targetUrl is null) return Results.NotFound();

    try
    {
        var response = await client.GetAsync(targetUrl);
        var content = await response.Content.ReadAsStringAsync();
        var doc = System.Text.Json.Nodes.JsonNode.Parse(content)?.AsObject();
        if (doc != null)
        {
            doc["security"] = new System.Text.Json.Nodes.JsonArray
            {
                new System.Text.Json.Nodes.JsonObject
                {
                    ["Bearer"] = new System.Text.Json.Nodes.JsonArray()
                }
            };
            return Results.Content(doc.ToJsonString(), "application/json");
        }
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to fetch swagger from {targetUrl}: {ex.Message}");
    }
}).ExcludeFromDescription().AllowAnonymous();

app.MapGet("/", (HttpContext context) =>
{
    var accept = context.Request.Headers.Accept.ToString();
    if (accept.Contains("application/json") && !accept.Contains("text/html"))
    {
        return Results.Ok(new
        {
            service = "api-gateway",
            framework = "YARP (Yet Another Reverse Proxy)",
            springCloudEquivalent = "Spring Cloud Gateway",
            status = "healthy",
            swagger = "/swagger",
            postgresUi = "http://localhost:8086",
            redisUi = "http://localhost:8087",
            rabbitmqUi = "http://localhost:15672",
            seqUi = "http://localhost:5341",
            jaegerUi = "http://localhost:16186",
            minioConsole = "http://localhost:8080/minio-console/",
            minioConsoleDirect = "http://localhost:9001",
            minioApiDirect = "http://localhost:9000",
            minioApiProxied = "http://localhost:8080/s3"
        });
    }

    return Results.Content(HtmlPortal.Content, "text/html");
}).ExcludeFromDescription().AllowAnonymous();

app.MapHealthChecks("/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => false
}).AllowAnonymous();

app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => true
}).AllowAnonymous();

app.MapReverseProxy();

app.Run();


