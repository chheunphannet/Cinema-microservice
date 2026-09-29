using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cinema.Foundation.Messaging;
using Cinema.Foundation.Security;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Identity.Api.Security;
using Identity.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Cinema.UnitTests;

public class Phase4CustomerSocialAuthTests
{
    private readonly Mock<IIdentityRepository> _mockRepo = new();
    private readonly Mock<IJwtTokenService> _mockJwtService = new();
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService = new();
    private readonly Mock<IGoogleTokenValidator> _mockGoogleValidator = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<ILogger<CustomerAuthService>> _mockLogger = new();

    private CustomerAuthService CreateService()
    {
        return new CustomerAuthService(
            _mockRepo.Object,
            _mockJwtService.Object,
            _mockRefreshTokenService.Object,
            _mockGoogleValidator.Object,
            _mockEventBus.Object,
            _mockLogger.Object
        );
    }

    private static string CreateTestJwt(string sub, string email, string name, bool emailVerified = true)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSecretKeyForJwtUnitTestingOnly123!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", sub),
                new Claim("email", email),
                new Claim("name", name),
                new Claim("given_name", name.Split(' ')[0]),
                new Claim("family_name", name.Contains(' ') ? name.Split(' ')[1] : ""),
                new Claim("picture", "https://avatar.example.com/photo.jpg"),
                new Claim("email_verified", emailVerified.ToString().ToLowerInvariant())
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static string CreateExpiredTestJwt(string sub, string email, string name)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSecretKeyForJwtUnitTestingOnly123!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", sub),
                new Claim("email", email),
                new Claim("name", name),
                new Claim("email_verified", "true")
            }),
            NotBefore = DateTime.UtcNow.AddHours(-3),
            IssuedAt = DateTime.UtcNow.AddHours(-3),
            Expires = DateTime.UtcNow.AddHours(-2),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    #region GoogleTokenValidator Tests

    [Fact]
    public async Task GoogleTokenValidator_ReturnsPayload_WhenValidMockJwtProvided()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "mock"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var httpClient = new System.Net.Http.HttpClient();
        var validator = new GoogleTokenValidator(httpClient, config, logger.Object);

        var jwt = CreateTestJwt("google-sub-12345", "alex@example.com", "Alex Smith", true);

        var payload = await validator.ValidateAsync(jwt);

        Assert.NotNull(payload);
        Assert.Equal("google-sub-12345", payload.Subject);
        Assert.Equal("alex@example.com", payload.Email);
        Assert.Equal("Alex Smith", payload.Name);
        Assert.Equal("Alex", payload.GivenName);
        Assert.Equal("Smith", payload.FamilyName);
        Assert.Equal("https://avatar.example.com/photo.jpg", payload.Picture);
        Assert.True(payload.EmailVerified);
    }

    [Fact]
    public async Task GoogleTokenValidator_ParsesEmailVerifiedFalse()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = ""
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var jwt = CreateTestJwt("google-sub-999", "unverified@example.com", "Unverified User", false);

        var payload = await validator.ValidateAsync(jwt);

        Assert.NotNull(payload);
        Assert.False(payload.EmailVerified);
    }

    [Fact]
    public async Task GoogleTokenValidator_ReturnsPayload_WhenJsonMockTokenProvided()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "dev"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var json = "{\"sub\": \"json-sub-456\", \"email\": \"json@example.com\", \"name\": \"Json User\", \"picture\": \"https://pic.url\", \"email_verified\": true}";

        var payload = await validator.ValidateAsync(json);

        Assert.NotNull(payload);
        Assert.Equal("json-sub-456", payload.Subject);
        Assert.Equal("json@example.com", payload.Email);
        Assert.Equal("Json User", payload.Name);
        Assert.True(payload.EmailVerified);
    }

    [Fact]
    public async Task GoogleTokenValidator_ReturnsPayload_WhenPipeDelimitedMockTokenProvided()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "mock"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var token = "mock-google-id-789|pipe@example.com|Pipe User|https://avatar.url|true";

        var payload = await validator.ValidateAsync(token);

        Assert.NotNull(payload);
        Assert.Equal("mock-google-id-789", payload.Subject);
        Assert.Equal("pipe@example.com", payload.Email);
        Assert.Equal("Pipe User", payload.Name);
        Assert.True(payload.EmailVerified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GoogleTokenValidator_ReturnsNull_WhenTokenEmptyOrNull(string? token)
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var payload = await validator.ValidateAsync(token!);

        Assert.Null(payload);
    }

    [Fact]
    public async Task GoogleTokenValidator_ReturnsNull_WhenTokenIsMalformedGarbage()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "mock"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var payload = await validator.ValidateAsync("random_malformed_token_string_not_valid");

        Assert.Null(payload);
    }

    [Fact]
    public async Task GoogleTokenValidator_RejectsMockToken_WhenInLiveProductionMode()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "production-client-id-12345.apps.googleusercontent.com"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        // Security check: in live production mode, mock- prefix must NEVER bypass validation
        var payload = await validator.ValidateAsync("mock-attacker-id|victim@example.com|Attacker|https://avatar.url|true");

        Assert.Null(payload);
    }

    [Fact]
    public async Task GoogleTokenValidator_RejectsExpiredJwt_InMockMode()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "mock"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        var expiredJwt = CreateExpiredTestJwt("google-sub-exp", "expired@example.com", "Expired User");

        var payload = await validator.ValidateAsync(expiredJwt);

        Assert.Null(payload);
    }

    [Fact]
    public async Task GoogleTokenValidator_RejectsExpiredJsonToken_InMockMode()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "dev"
        }).Build();

        var logger = new Mock<ILogger<GoogleTokenValidator>>();
        var validator = new GoogleTokenValidator(new System.Net.Http.HttpClient(), config, logger.Object);

        long pastUnix = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var json = $"{{\"sub\": \"json-sub-exp\", \"email\": \"exp@example.com\", \"name\": \"Exp User\", \"exp\": {pastUnix}, \"email_verified\": true}}";

        var payload = await validator.ValidateAsync(json);

        Assert.Null(payload);
    }

    #endregion

    #region GoogleLoginAsync Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GoogleLoginAsync_ReturnsBadRequest_WhenIdTokenEmpty(string? token)
    {
        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest(token!));

        var badRequest = result as IStatusCodeHttpResult;
        Assert.NotNull(badRequest);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenTokenValidatorReturnsNull()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("invalid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleTokenPayload?)null);

        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("invalid-token"));

        var unauthorized = result as IStatusCodeHttpResult;
        Assert.NotNull(unauthorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsBadRequest_WhenEmailNotVerified()
    {
        var unverifiedPayload = new GoogleTokenPayload(
            Subject: "google-123",
            Email: "unverified@example.com",
            Name: "Test User",
            GivenName: "Test",
            FamilyName: "User",
            Picture: null,
            EmailVerified: false
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("valid-unverified-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unverifiedPayload);

        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("valid-unverified-token"));

        var badRequest = result as IStatusCodeHttpResult;
        Assert.NotNull(badRequest);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsExistingCustomer_WhenCustomerExistsByGoogleId()
    {
        var customerId = Guid.NewGuid();
        var payload = new GoogleTokenPayload(
            Subject: "google-existing-id",
            Email: "existing@example.com",
            Name: "Existing GoogleUser",
            GivenName: "Existing",
            FamilyName: "GoogleUser",
            Picture: "https://avatar.url/ex.jpg",
            EmailVerified: true
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("existing-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        dynamic existingDbCustomer = new System.Dynamic.ExpandoObject();
        existingDbCustomer.customer_id = customerId;
        existingDbCustomer.email = "existing@example.com";
        existingDbCustomer.first_name = "Existing";
        existingDbCustomer.last_name = "GoogleUser";
        existingDbCustomer.avatar_url = "https://avatar.url/ex.jpg";

        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-existing-id"))
            .ReturnsAsync((object)existingDbCustomer);

        _mockJwtService
            .Setup(j => j.GenerateToken(customerId, "existing@example.com", "Existing GoogleUser", "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Returns("mock-jwt-token-123");

        _mockRefreshTokenService
            .Setup(r => r.GenerateRefreshTokenAsync(customerId, "existing@example.com", "customer", null))
            .ReturnsAsync("mock-refresh-token-456");

        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("existing-token"));

        var okResult = result as Ok<GoogleLoginResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(customerId, okResult.Value.CustomerId);
        Assert.Equal("mock-jwt-token-123", okResult.Value.Token);
        Assert.Equal("mock-refresh-token-456", okResult.Value.RefreshToken);
        Assert.Equal("existing@example.com", okResult.Value.Email);
        Assert.Equal("google", okResult.Value.AuthProvider);

        // Verify no creation or linking occurred
        _mockRepo.Verify(r => r.CreateGoogleCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockRepo.Verify(r => r.LinkGoogleAccountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockEventBus.Verify(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLoginAsync_LinksAccount_WhenCustomerExistsByEmail()
    {
        var customerId = Guid.NewGuid();
        var payload = new GoogleTokenPayload(
            Subject: "google-new-id",
            Email: "linked@example.com",
            Name: "Linked User",
            GivenName: "Linked",
            FamilyName: "User",
            Picture: "https://avatar.url/new.jpg",
            EmailVerified: true
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("link-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        // Not found by google_id
        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-new-id"))
            .ReturnsAsync((object?)null);

        // Found by email (e.g. registered locally previously)
        dynamic existingDbCustomer = new System.Dynamic.ExpandoObject();
        existingDbCustomer.customer_id = customerId;
        existingDbCustomer.email = "linked@example.com";
        existingDbCustomer.first_name = "Linked";
        existingDbCustomer.last_name = "User";
        existingDbCustomer.avatar_url = null;

        _mockRepo
            .Setup(r => r.GetCustomerByEmailAsync("linked@example.com"))
            .ReturnsAsync((object)existingDbCustomer);

        _mockRepo
            .Setup(r => r.LinkGoogleAccountAsync(customerId, "google-new-id", "https://avatar.url/new.jpg"))
            .Returns(Task.CompletedTask);

        _mockJwtService
            .Setup(j => j.GenerateToken(customerId, "linked@example.com", "Linked User", "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Returns("mock-jwt-token-linked");

        _mockRefreshTokenService
            .Setup(r => r.GenerateRefreshTokenAsync(customerId, "linked@example.com", "customer", null))
            .ReturnsAsync("mock-refresh-linked");

        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("link-token"));

        var okResult = result as Ok<GoogleLoginResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(customerId, okResult.Value.CustomerId);
        Assert.Equal("mock-jwt-token-linked", okResult.Value.Token);
        Assert.Equal("google", okResult.Value.AuthProvider);

        // Verify account linking was called with avatar and google ID
        _mockRepo.Verify(r => r.LinkGoogleAccountAsync(customerId, "google-new-id", "https://avatar.url/new.jpg"), Times.Once);
        // Verify customer was NOT created again
        _mockRepo.Verify(r => r.CreateGoogleCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        // Verify registration event was NOT published (already registered)
        _mockEventBus.Verify(e => e.PublishAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLoginAsync_CreatesNewCustomerAndPublishesEvent_WhenCustomerNotFound()
    {
        var newCustomerId = Guid.NewGuid();
        var payload = new GoogleTokenPayload(
            Subject: "google-brand-new",
            Email: "newcomer@example.com",
            Name: "Brand Newcomer",
            GivenName: "Brand",
            FamilyName: "Newcomer",
            Picture: "https://avatar.url/newcomer.jpg",
            EmailVerified: true
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("brand-new-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-brand-new"))
            .ReturnsAsync((object?)null);

        _mockRepo
            .Setup(r => r.GetCustomerByEmailAsync("newcomer@example.com"))
            .ReturnsAsync((object?)null);

        _mockRepo
            .Setup(r => r.CreateGoogleCustomerAsync("newcomer@example.com", "Brand", "Newcomer", "google-brand-new", "https://avatar.url/newcomer.jpg"))
            .ReturnsAsync(newCustomerId);

        _mockJwtService
            .Setup(j => j.GenerateToken(newCustomerId, "newcomer@example.com", "Brand Newcomer", "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Returns("mock-jwt-new");

        _mockRefreshTokenService
            .Setup(r => r.GenerateRefreshTokenAsync(newCustomerId, "newcomer@example.com", "customer", null))
            .ReturnsAsync("mock-refresh-new");

        var service = CreateService();

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("brand-new-token"));

        var okResult = result as Ok<GoogleLoginResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(newCustomerId, okResult.Value.CustomerId);
        Assert.Equal("newcomer@example.com", okResult.Value.Email);
        Assert.Equal("Brand", okResult.Value.FirstName);
        Assert.Equal("Newcomer", okResult.Value.LastName);
        Assert.Equal("https://avatar.url/newcomer.jpg", okResult.Value.AvatarUrl);
        Assert.Equal("google", okResult.Value.AuthProvider);

        // Verify creation in DB
        _mockRepo.Verify(r => r.CreateGoogleCustomerAsync("newcomer@example.com", "Brand", "Newcomer", "google-brand-new", "https://avatar.url/newcomer.jpg"), Times.Once);

        // Verify CustomerRegisteredIntegrationEvent was published to RabbitMQ
        _mockEventBus.Verify(e => e.PublishAsync(
            "customer.registered",
            It.Is<CustomerRegisteredIntegrationEvent>(evt =>
                evt.CustomerId == newCustomerId &&
                evt.Email == "newcomer@example.com" &&
                evt.FirstName == "Brand" &&
                evt.LastName == "Newcomer")
        ), Times.Once);
    }

    [Fact]
    public async Task GoogleLoginAsync_HandlesLongNames_TruncatesTo100Chars()
    {
        var longFirstName = new string('A', 150);
        var longLastName = new string('B', 150);
        var payload = new GoogleTokenPayload(
            Subject: "google-long-names",
            Email: "longname@example.com",
            Name: $"{longFirstName} {longLastName}",
            GivenName: longFirstName,
            FamilyName: longLastName,
            Picture: null,
            EmailVerified: true
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("long-name-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-long-names"))
            .ReturnsAsync((object?)null);
        _mockRepo
            .Setup(r => r.GetCustomerByEmailAsync("longname@example.com"))
            .ReturnsAsync((object?)null);

        string? capturedFirst = null;
        string? capturedLast = null;
        _mockRepo
            .Setup(r => r.CreateGoogleCustomerAsync("longname@example.com", It.IsAny<string>(), It.IsAny<string>(), "google-long-names", null))
            .Callback<string, string, string, string, string?>((e, f, l, g, p) =>
            {
                capturedFirst = f;
                capturedLast = l;
            })
            .ReturnsAsync(Guid.NewGuid());

        var service = CreateService();
        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("long-name-token"));

        var okResult = result as Ok<GoogleLoginResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(capturedFirst);
        Assert.NotNull(capturedLast);
        Assert.Equal(100, capturedFirst.Length);
        Assert.Equal(100, capturedLast.Length);
    }

    [Fact]
    public async Task GoogleLoginAsync_RecoversGracefully_WhenConcurrentRegistrationConflictOccurs()
    {
        var customerId = Guid.NewGuid();
        var payload = new GoogleTokenPayload(
            Subject: "google-concurrent",
            Email: "concurrent@example.com",
            Name: "Concurrent User",
            GivenName: "Concurrent",
            FamilyName: "User",
            Picture: null,
            EmailVerified: true
        );

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("concurrent-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-concurrent"))
            .ReturnsAsync((object?)null);
        _mockRepo
            .Setup(r => r.GetCustomerByEmailAsync("concurrent@example.com"))
            .ReturnsAsync((object?)null);

        // Simulate concurrent conflict (23505 unique violation)
        _mockRepo
            .Setup(r => r.CreateGoogleCustomerAsync("concurrent@example.com", "Concurrent", "User", "google-concurrent", null))
            .ThrowsAsync(new Exception("23505: duplicate key value violates unique constraint"));

        dynamic concurrentCustomer = new System.Dynamic.ExpandoObject();
        concurrentCustomer.customer_id = customerId;
        concurrentCustomer.email = "concurrent@example.com";
        concurrentCustomer.first_name = "Concurrent";
        concurrentCustomer.last_name = "User";
        concurrentCustomer.avatar_url = null;

        // When caught, CustomerAuthService queries GetCustomerByGoogleIdAsync or GetCustomerByEmailAsync again
        _mockRepo
            .Setup(r => r.GetCustomerByGoogleIdAsync("google-concurrent"))
            .ReturnsAsync((object)concurrentCustomer);

        _mockJwtService
            .Setup(j => j.GenerateToken(customerId, "concurrent@example.com", "Concurrent User", "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Returns("token-concurrent-recovered");

        var service = CreateService();
        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("concurrent-token"));

        var okResult = result as Ok<GoogleLoginResponse>;
        Assert.NotNull(okResult);
        Assert.Equal(customerId, okResult.Value?.CustomerId);
        Assert.Equal("token-concurrent-recovered", okResult.Value?.Token);
    }

    #endregion

    #region Token Generation & Claims Verification

    [Fact]
    public void JwtTokenService_GeneratesStandardRs256OrHmacTokenWithExpectedClaims()
    {
        var jwtService = new JwtTokenService(
            secretKey: "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes",
            issuer: "cinemapos",
            audience: "cinemapos-clients"
        );

        var userId = Guid.NewGuid();
        var extraClaims = new[]
        {
            new Claim("email", "john@example.com"),
            new Claim("auth_provider", "google")
        };

        var tokenString = jwtService.GenerateToken(
            userId: userId,
            username: "john@example.com",
            displayName: "John Doe",
            role: "customer",
            branchId: null,
            extraClaims: extraClaims
        );

        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var principal = jwtService.ValidateToken(tokenString);
        Assert.NotNull(principal);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(userId.ToString(), subClaim);

        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
        Assert.Equal("customer", roleClaim);

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
        Assert.Equal("john@example.com", emailClaim);

        var authProviderClaim = principal.FindFirst("auth_provider")?.Value;
        Assert.Equal("google", authProviderClaim);

        // Also verify directly against raw JWT payload claims
        var rawJwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
        Assert.Equal(userId.ToString(), rawJwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value);
        Assert.Equal("john@example.com", rawJwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value);
        Assert.Equal("google", rawJwt.Claims.FirstOrDefault(c => c.Type == "auth_provider")?.Value);
        Assert.Equal("customer", rawJwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshTokenAsync_ReturnsBadRequest_WhenTokenEmpty()
    {
        var service = CreateService();

        var result = await service.RefreshTokenAsync(new RefreshTokenRequest(""));

        var badRequest = result as IStatusCodeHttpResult;
        Assert.NotNull(badRequest);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsUnauthorized_WhenRotationFails()
    {
        _mockRefreshTokenService
            .Setup(r => r.RotateRefreshTokenAsync("invalid-token"))
            .ReturnsAsync((RefreshTokenResult?)null);

        var service = CreateService();

        var result = await service.RefreshTokenAsync(new RefreshTokenRequest("invalid-token"));

        var unauthorized = result as IStatusCodeHttpResult;
        Assert.NotNull(unauthorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNewTokens_WhenValid()
    {
        var userId = Guid.NewGuid();
        var rotateResult = new RefreshTokenResult(userId, "rotated@example.com", "customer", null);

        _mockRefreshTokenService
            .Setup(r => r.RotateRefreshTokenAsync("valid-refresh-token"))
            .ReturnsAsync(rotateResult);

        _mockJwtService
            .Setup(j => j.GenerateToken(userId, "rotated@example.com", "rotated@example.com", "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Returns("new-access-token");

        _mockRefreshTokenService
            .Setup(r => r.GenerateRefreshTokenAsync(userId, "rotated@example.com", "customer", null))
            .ReturnsAsync("new-refresh-token");

        var service = CreateService();

        var result = await service.RefreshTokenAsync(new RefreshTokenRequest("valid-refresh-token"));

        var okResult = result as Ok<CustomerTokenRefreshResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("new-access-token", okResult.Value.Token);
        Assert.Equal("new-refresh-token", okResult.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_PreservesCustomerAuthProviderAndDisplayName()
    {
        var userId = Guid.NewGuid();
        var rotateResult = new RefreshTokenResult(userId, "localuser@example.com", "customer", null);

        _mockRefreshTokenService
            .Setup(r => r.RotateRefreshTokenAsync("valid-local-refresh-token"))
            .ReturnsAsync(rotateResult);

        dynamic localCustomer = new System.Dynamic.ExpandoObject();
        localCustomer.customer_id = userId;
        localCustomer.email = "localuser@example.com";
        localCustomer.first_name = "Alice";
        localCustomer.last_name = "Wonderland";
        localCustomer.auth_provider = "local";

        _mockRepo
            .Setup(r => r.GetCustomerByEmailAsync("localuser@example.com"))
            .ReturnsAsync((object)localCustomer);

        IEnumerable<Claim>? capturedClaims = null;
        string? capturedDisplayName = null;
        _mockJwtService
            .Setup(j => j.GenerateToken(userId, "localuser@example.com", It.IsAny<string>(), "customer", null, null, It.IsAny<IEnumerable<Claim>>()))
            .Callback<Guid, string, string, string, Guid?, TimeSpan?, IEnumerable<Claim>?>((u, un, dn, r, b, exp, claims) =>
            {
                capturedDisplayName = dn;
                capturedClaims = claims;
            })
            .Returns("new-access-token-local");

        _mockRefreshTokenService
            .Setup(r => r.GenerateRefreshTokenAsync(userId, "localuser@example.com", "customer", null))
            .ReturnsAsync("new-refresh-token-local");

        var service = CreateService();

        var result = await service.RefreshTokenAsync(new RefreshTokenRequest("valid-local-refresh-token"));

        var okResult = result as Ok<CustomerTokenRefreshResponse>;
        Assert.NotNull(okResult);
        Assert.Equal("Alice Wonderland", capturedDisplayName);
        Assert.NotNull(capturedClaims);
        var authProviderClaim = capturedClaims.FirstOrDefault(c => c.Type == "auth_provider")?.Value;
        Assert.Equal("local", authProviderClaim);
    }

    #endregion

    #region Loyalty Consumer Provisioning Tests

    [Fact]
    public void CustomerRegisteredIntegrationEvent_SerializationAndVoucherCodeFormat_AreCorrect()
    {
        var customerId = Guid.NewGuid();
        var email = "loyalty.new@example.com";
        var evt = new CustomerRegisteredIntegrationEvent(
            CustomerId: customerId,
            Email: email,
            FirstName: "Loyalty",
            LastName: "Member",
            Phone: "+1234567890",
            RegisteredAt: DateTimeOffset.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(evt);

        // Verify JSON payload contains expected fields
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("CustomerId", out var idProp));
        Assert.Equal(customerId, idProp.GetGuid());
        Assert.True(root.TryGetProperty("Email", out var emailProp));
        Assert.Equal(email, emailProp.GetString());

        // Verify expected voucher code format
        string expectedVoucherCode = $"WELCOME-{customerId.ToString("N")[..8].ToUpperInvariant()}";
        Assert.StartsWith("WELCOME-", expectedVoucherCode);
        Assert.Equal(16, expectedVoucherCode.Length);
    }

    #endregion
}
