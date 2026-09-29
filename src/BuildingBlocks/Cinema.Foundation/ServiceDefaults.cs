using System.Text;
using Cinema.Foundation.Data;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Redis;
using Cinema.Foundation.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

namespace Cinema.Foundation;

public static class ServiceDefaults
{
    public static WebApplication ConfigureCinemaService(
        this WebApplicationBuilder builder, 
        string serviceName, 
        string serviceDescription = "Cinema POS Microservice")
    {
        // Support Docker Secrets
        builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);

        // Add Serilog with Seq sink
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

        builder.Services.AddHealthChecks();
        builder.Services.AddProblemDetails();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000", "http://localhost:8080", "http://localhost:4321", "http://127.0.0.1:4321" };
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS");
        }));

        // Register Dapper TypeHandlers for DateOnly
        Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        Dapper.SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());

        // OpenTelemetry Metrics & Prometheus Exporter & Tracing (Module 3.2 Operability)
        var otelServiceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? serviceName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: otelServiceName,
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
                    .AddMeter("Cinema.Pos.Telemetry")
                    .AddPrometheusExporter()
                    .AddOtlpExporter();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddOtlpExporter();
            });

        // Database connections
        var writeDb = builder.Configuration.GetConnectionString("WriteDb");
        var readDb = builder.Configuration.GetConnectionString("ReadDb");
        builder.Services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(writeDb, readDb));

        // Redis & Distributed Locking
        var redisConfig = builder.Configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConfig))
        {
            try
            {
                var options = ConfigurationOptions.Parse(redisConfig);
                options.AbortOnConnectFail = false;
                var multiplexer = ConnectionMultiplexer.Connect(options);
                builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }
            catch
            {
                // Fallback handled gracefully
            }

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConfig;
                options.InstanceName = $"{serviceName}:";
            });
        }
        else
        {
            builder.Services.AddDistributedMemoryCache();
        }

        builder.Services.AddSingleton<ISeatLockService, SeatLockService>();
        builder.Services.AddSingleton<ITokenRevocationService, TokenRevocationService>();
        builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // RabbitMQ Asynchronous Event Bus (Module 1 & Deliverable Weeks 3-5)
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "cinema_app";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"];
        if (string.IsNullOrWhiteSpace(rabbitPass))
            throw new InvalidOperationException("RabbitMQ Password is required in configuration.");
        int rabbitPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        var appName = builder.Environment.ApplicationName ?? "cinema-service";
        builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitHost, rabbitUser, rabbitPass, rabbitPort, clientProvidedName: appName, logger: sp.GetService<ILogger<RabbitMqEventBus>>()));

        var rabbitConnStr = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}";
        builder.Services.AddHealthChecks()
            .AddNpgSql(writeDb!)
            .AddRedis(redisConfig!)
            .AddRabbitMQ(sp => 
            {
                var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitConnStr) };
                return factory.CreateConnectionAsync();
            });

        // JWT Authentication & Authorization
        var jwtSecret = builder.Configuration["Jwt:Secret"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env != "Development")
        {
            if (string.IsNullOrWhiteSpace(jwtSecret) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
            {
                throw new InvalidOperationException("Production block: JWT Secret, Issuer, and Audience must be explicitly configured.");
            }
        }
        else
        {
            jwtSecret ??= JwtTokenService.DefaultSecretKey;
            jwtIssuer ??= JwtTokenService.DefaultIssuer;
            jwtAudience ??= JwtTokenService.DefaultAudience;
        }

        builder.Services.TryAddSingleton<IJwtTokenService>(new JwtTokenService(jwtSecret, jwtIssuer, jwtAudience));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Retrieve Metadata (JWKS) from Identity.Api
                options.MetadataAddress = "http://identity-api:8080/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                        if (!string.IsNullOrEmpty(jti))
                        {
                            var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
                            var isRevoked = await tokenRevocationService.IsRevokedAsync(jti);
                            if (isRevoked)
                            {
                                context.Fail("This token has been revoked.");
                            }
                        }
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdmin", policy => policy.RequireRole("super_admin", "system_admin"));
            options.AddPolicy("BranchManager", policy => policy.RequireRole("branch_manager", "super_admin", "system_admin"));
            options.AddPolicy("ContentManager", policy => policy.RequireRole("content_manager", "super_admin", "system_admin"));
            options.AddPolicy("InventoryManager", policy => policy.RequireRole("inventory_manager", "super_admin", "system_admin"));
            options.AddPolicy("FinanceManager", policy => policy.RequireRole("finance_manager", "super_admin", "system_admin"));
            options.AddPolicy("MarketingManager", policy => policy.RequireRole("marketing_manager", "super_admin", "system_admin"));
            options.AddPolicy("CustomerSupport", policy => policy.RequireRole("customer_support", "finance_manager", "branch_manager", "super_admin", "system_admin"));
            options.AddPolicy("Staff", policy => policy.RequireRole("staff", "cashier", "supervisor", "branch_manager", "super_admin", "system_admin"));
            
            // Legacy / convenience policies with enterprise role mappings
            options.AddPolicy("Cashier", policy => policy.RequireRole("cashier", "staff", "supervisor", "branch_manager", "super_admin", "system_admin"));
            options.AddPolicy("Supervisor", policy => policy.RequireRole("supervisor", "branch_manager", "super_admin", "system_admin"));
            options.AddPolicy("SystemAdmin", policy => policy.RequireRole("system_admin", "super_admin"));
            options.AddPolicy("BookingsRead", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "bookings.read" || c.Value == "*.*"))));
            options.AddPolicy("BookingsRefund", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "bookings.refund" || c.Value == "*.*"))));
            options.AddPolicy("PricingWrite", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "pricing.write" || c.Value == "*.*"))));
            options.AddPolicy("InventoryAdjust", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "inventory.adjust" || c.Value == "*.*"))));
            options.AddPolicy("ReportsRead", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "reports.read" || c.Value == "*.*"))));
            options.AddPolicy("StaffWrite", policy => policy.RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == "permission" && (c.Value == "staff.write" || c.Value == "*.*"))));
        });

        // OpenAPI / Swagger Documentation with Bearer Scheme
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = $"Cinema POS — {char.ToUpper(serviceName[0]) + serviceName[1..]} Service API",
                Version = "v1",
                Description = $"{serviceDescription}\n\n" +
                              "Compliant with Phase 1 Technical Architecture Specification: Multi-Branch Cinema POS & Ticketing System.\n" +
                              "Architecture: ASP.NET Core Microservices + PostgreSQL (Write Primary / Read Replica) + Redis LRU Cache & Distributed Locking.",
                Contact = new OpenApiContact
                {
                    Name = "Cinema POS Architecture Team",
                    Email = "support@cinemapos.local"
                }
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddSingleton(new ServiceIdentity(
            serviceName,
            writeDb,
            readDb,
            redisConfig));

        return builder.Build();
    }

    public static WebApplication MapFoundationEndpoints(this WebApplication app, string serviceName)
    {
        app.UseExceptionHandler();
        app.UseCors();

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

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<WebhookSignatureMiddleware>();

        // Prometheus Metrics Scraper Endpoint (Module 3.2 & Module 5.1)
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        // Enable Swagger & Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName.ToUpperInvariant()} API v1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = $"{char.ToUpper(serviceName[0]) + serviceName[1..]} Service API Docs";
        });

        app.MapHealthChecks("/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = r => false
        })
        .WithTags("Health & Diagnostics")
        .WithSummary("Liveness Probe")
        .AllowAnonymous();

        app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = r => true
        })
        .WithTags("Health & Diagnostics")
        .WithSummary("Readiness Probe")
        .AllowAnonymous();

        // Root endpoint: Redirect to Swagger UI for browser navigation, or return JSON status
        app.MapGet("/", (HttpContext context, ServiceIdentity identity) =>
        {
            var accept = context.Request.Headers.Accept.ToString();
            if (accept.Contains("text/html") && !accept.Contains("application/json"))
            {
                return Results.Redirect("/swagger");
            }

            return Results.Ok(new
            {
                service = serviceName,
                status = "ready",
                utc = DateTimeOffset.UtcNow,
                documentation = "/swagger",
                openapi = "/swagger/v1/swagger.json",
                health = "/health",
                metrics = "/metrics",
                writeDatabaseConfigured = !string.IsNullOrWhiteSpace(identity.WriteDatabase),
                readDatabaseConfigured = !string.IsNullOrWhiteSpace(identity.ReadDatabase),
                redisConfigured = !string.IsNullOrWhiteSpace(identity.Redis)
            });
        }).ExcludeFromDescription()
          .AllowAnonymous();

        return app;
    }
}

public sealed record ServiceIdentity(string Name, string? WriteDatabase, string? ReadDatabase, string? Redis);


