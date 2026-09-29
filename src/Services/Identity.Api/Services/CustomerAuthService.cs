using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Security;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Identity.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Api.Services;

public interface ICustomerAuthService
{
    Task<IResult> RegisterAsync(CustomerRegisterRequest request);
    Task<IResult> LoginAsync(CustomerLoginRequest request);
    Task<IResult> GoogleLoginAsync(GoogleLoginRequest request);
    Task<IResult> RefreshTokenAsync(RefreshTokenRequest request);
}

public class CustomerAuthService : ICustomerAuthService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private readonly IIdentityRepository _repository;
    private readonly IJwtTokenService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CustomerAuthService> _logger;

    public CustomerAuthService(
        IIdentityRepository repository,
        IJwtTokenService jwtService,
        IRefreshTokenService refreshTokenService,
        IGoogleTokenValidator googleTokenValidator,
        IEventBus eventBus,
        ILogger<CustomerAuthService> logger)
    {
        _repository = repository;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _googleTokenValidator = googleTokenValidator;
        _eventBus = eventBus;
        _logger = logger;
    }

    private static IResult BadRequestJson(string error) =>
        Results.Content(
            System.Text.Json.JsonSerializer.Serialize(new { error }),
            "application/json",
            System.Text.Encoding.UTF8,
            StatusCodes.Status400BadRequest
        );

    private static IResult ConflictJson(string error) =>
        Results.Content(
            System.Text.Json.JsonSerializer.Serialize(new { error }),
            "application/json",
            System.Text.Encoding.UTF8,
            StatusCodes.Status409Conflict
        );

    public async Task<IResult> RegisterAsync(CustomerRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !EmailRegex.IsMatch(request.Email))
        {
            return BadRequestJson("Invalid email format.");
        }
        
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequestJson("Password must be at least 8 characters long.");
        }

        var existing = await _repository.GetCustomerByEmailAsync(request.Email);
        if (existing != null)
        {
            return ConflictJson("Email is already registered.");
        }

        string hash = PasswordHasher.Hash(request.Password);
        
        Guid customerId = await _repository.CreateCustomerAsync(request.Email, hash, request.FirstName, request.LastName, request.Phone);

        try
        {
            await _eventBus.PublishAsync("customer.registered", new CustomerRegisteredIntegrationEvent(
                CustomerId: customerId,
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Phone: request.Phone,
                RegisteredAt: DateTimeOffset.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish customer.registered event for {Id}", customerId);
        }

        return Results.Ok(new { customerId, message = "Registration successful." });
    }

    public async Task<IResult> LoginAsync(CustomerLoginRequest request)
    {
        var customer = await _repository.GetCustomerByEmailAsync(request.Email);
        if (customer == null)
        {
            return Results.Unauthorized();
        }

        string? storedHash = (string?)customer.password_hash;
        if (string.IsNullOrEmpty(storedHash) || !PasswordHasher.Verify(request.Password, storedHash))
        {
            return Results.Unauthorized();
        }

        Guid customerId = (Guid)customer.customer_id;
        
        var extraClaims = new[]
        {
            new Claim("email", (string)customer.email),
            new Claim("auth_provider", (string)(customer.auth_provider ?? "local"))
        };

        var token = _jwtService.GenerateToken(
            userId: customerId,
            username: request.Email,
            displayName: $"{customer.first_name} {customer.last_name}",
            role: "customer",
            branchId: null,
            extraClaims: extraClaims
        );

        string? refreshToken = null;
        try
        {
            refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(customerId, request.Email, "customer", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate refresh token for customer {Id}", customerId);
        }

        return Results.Ok(new CustomerLoginResponse(
            Token: token,
            CustomerId: customerId,
            Email: (string)customer.email,
            FirstName: (string)customer.first_name,
            LastName: (string)customer.last_name,
            RefreshToken: refreshToken
        ));
    }

    public async Task<IResult> GoogleLoginAsync(GoogleLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequestJson("Google ID token is required.");
        }

        var payload = await _googleTokenValidator.ValidateAsync(request.IdToken.Trim());
        if (payload == null)
        {
            return Results.Unauthorized();
        }

        if (!payload.EmailVerified)
        {
            return BadRequestJson("Google account email is not verified.");
        }

        Guid customerId;
        string firstName;
        string lastName;
        string? avatarUrl;
        string email = payload.Email.Trim().ToLowerInvariant();

        // 1. Check if customer exists by google_id
        var existingByGoogle = await _repository.GetCustomerByGoogleIdAsync(payload.Subject);
        if (existingByGoogle != null)
        {
            customerId = (Guid)existingByGoogle.customer_id;
            firstName = (string?)existingByGoogle.first_name ?? "Customer";
            lastName = (string?)existingByGoogle.last_name ?? "";
            avatarUrl = (string?)existingByGoogle.avatar_url;
            var dbEmail = (string?)existingByGoogle.email;
            if (!string.IsNullOrWhiteSpace(dbEmail))
            {
                email = dbEmail.Trim().ToLowerInvariant();
            }
        }
        else
        {
            // 2. Check if customer exists by email
            var existingByEmail = await _repository.GetCustomerByEmailAsync(email);
            if (existingByEmail != null)
            {
                customerId = (Guid)existingByEmail.customer_id;
                firstName = (string?)existingByEmail.first_name ?? "Customer";
                lastName = (string?)existingByEmail.last_name ?? "";
                string? currentAvatar = (string?)existingByEmail.avatar_url;
                avatarUrl = string.IsNullOrWhiteSpace(currentAvatar) ? payload.Picture : currentAvatar;

                // Link account by setting google_id, update avatar_url if empty, update auth_provider = 'google'
                await _repository.LinkGoogleAccountAsync(customerId, payload.Subject, avatarUrl);
            }
            else
            {
                // 3. Customer does not exist: create new record
                firstName = payload.GivenName ?? "";
                lastName = payload.FamilyName ?? "";
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    var nameParts = (payload.Name ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    firstName = nameParts.Length > 0 ? nameParts[0] : "Customer";
                    lastName = nameParts.Length > 1 ? nameParts[1] : "";
                }
                if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
                {
                    firstName = "Customer";
                    lastName = "";
                }

                // Truncate to DB column length (VARCHAR(100))
                if (firstName.Length > 100) firstName = firstName[..100];
                if (lastName.Length > 100) lastName = lastName[..100];

                avatarUrl = payload.Picture;

                try
                {
                    customerId = await _repository.CreateGoogleCustomerAsync(
                        email: email,
                        firstName: firstName,
                        lastName: lastName,
                        googleId: payload.Subject,
                        avatarUrl: avatarUrl
                    );

                    // Publish CustomerRegisteredIntegrationEvent to RabbitMQ
                    try
                    {
                        await _eventBus.PublishAsync("customer.registered", new CustomerRegisteredIntegrationEvent(
                            CustomerId: customerId,
                            Email: email,
                            FirstName: firstName,
                            LastName: lastName,
                            Phone: null,
                            RegisteredAt: DateTimeOffset.UtcNow
                        ));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish customer.registered event for Google customer {Id}", customerId);
                    }
                }
                catch (Exception ex) when (
                    (ex is Npgsql.PostgresException pg && pg.SqlState == "23505") ||
                    (ex.InnerException is Npgsql.PostgresException innerPg && innerPg.SqlState == "23505") ||
                    ex.Message.Contains("23505") ||
                    ex.Message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase))
                {
                    // Concurrency guard: if another request registered this Google user or email simultaneously, fetch and recover
                    _logger.LogInformation("Concurrent registration conflict resolved for Google user {Sub} / {Email}", payload.Subject, email);
                    var recheck = await _repository.GetCustomerByGoogleIdAsync(payload.Subject)
                               ?? await _repository.GetCustomerByEmailAsync(email);
                    if (recheck != null)
                    {
                        customerId = (Guid)recheck.customer_id;
                        firstName = (string?)recheck.first_name ?? firstName;
                        lastName = (string?)recheck.last_name ?? lastName;
                        avatarUrl = (string?)recheck.avatar_url ?? avatarUrl;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        // Generate RS256 JWT Token with claims (sub/userId, email, name, role="customer", auth_provider="google")
        var displayName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = email;
        }

        var extraClaims = new[]
        {
            new Claim("email", email),
            new Claim("auth_provider", "google")
        };

        var token = _jwtService.GenerateToken(
            userId: customerId,
            username: email,
            displayName: displayName,
            role: "customer",
            branchId: null,
            extraClaims: extraClaims
        );

        string? refreshToken = null;
        try
        {
            refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(
                userId: customerId,
                username: email,
                role: "customer",
                branchId: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate refresh token for customer {Id}", customerId);
        }

        return Results.Ok(new GoogleLoginResponse(
            Token: token,
            RefreshToken: refreshToken,
            CustomerId: customerId,
            Email: email,
            FirstName: firstName,
            LastName: lastName,
            AvatarUrl: avatarUrl,
            AuthProvider: "google"
        ));
    }

    public async Task<IResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequestJson("Refresh token is required.");
        }

        var result = await _refreshTokenService.RotateRefreshTokenAsync(request.RefreshToken);
        if (result == null)
        {
            return Results.Unauthorized();
        }

        // Fetch customer profile to preserve actual auth_provider and display name
        string authProvider = "local";
        string displayName = result.Username;

        try
        {
            var customer = await _repository.GetCustomerByEmailAsync(result.Username);
            if (customer != null)
            {
                authProvider = (string?)customer.auth_provider ?? "local";
                var fullName = $"{customer.first_name} {customer.last_name}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    displayName = fullName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query customer profile for refreshed token {User}", result.Username);
        }

        var extraClaims = new[]
        {
            new Claim("email", result.Username),
            new Claim("auth_provider", authProvider)
        };

        var newAccessToken = _jwtService.GenerateToken(
            userId: result.UserId,
            username: result.Username,
            displayName: displayName,
            role: result.Role,
            branchId: result.BranchId,
            extraClaims: extraClaims
        );

        var newRefreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(
            userId: result.UserId,
            username: result.Username,
            role: result.Role,
            branchId: result.BranchId
        );

        return Results.Ok(new CustomerTokenRefreshResponse(
            Token: newAccessToken,
            RefreshToken: newRefreshToken
        ));
    }
}
