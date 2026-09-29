using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Cinema.Foundation.Email;
using Identity.Api.Models;
using Identity.Api.Repositories;
using Loyalty.Api.Models;
using Loyalty.Api.Repositories;
using Loyalty.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cinema.UnitTests;

public class AdminMarketingAndSystemTests
{
    private readonly Mock<IAdminLoyaltyRepository> _mockLoyaltyRepo = new();
    private readonly Mock<IEmailService> _mockEmailService = new();
    private readonly Mock<ILogger<AdminLoyaltyService>> _mockLogger = new();
    private readonly Mock<IAdminSystemRepository> _mockSystemRepo = new();

    private AdminLoyaltyService CreateLoyaltyService()
    {
        return new AdminLoyaltyService(
            _mockLoyaltyRepo.Object,
            _mockEmailService.Object,
            _mockLogger.Object
        );
    }

    private static ClaimsPrincipal CreateUser(string role, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // =========================================================================
    // 1. Loyalty Points Manual Adjustment Tests
    // =========================================================================

    [Fact]
    public async Task AdjustPoints_PositiveDelta_IncreasesBalance_AndReturnsAudit()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new AdjustPointsRequest(PointsDelta: 200, Reason: "Customer satisfaction compensation");

        var expectedResponse = new AdjustPointsResponse(
            MemberId: memberId,
            Email: "customer@example.com",
            PointsDelta: 200,
            NewBalance: 700,
            Reason: "Customer satisfaction compensation",
            AdjustedAt: DateTimeOffset.UtcNow
        );

        _mockLoyaltyRepo.Setup(r => r.AdjustMemberPointsAsync(
                memberId, 200, "Customer satisfaction compensation", null, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.AdjustMemberPointsAsync(memberId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<AdjustPointsResponse>>(result);
        Assert.Equal(700, okValue.Value!.NewBalance);
        Assert.Equal(200, okValue.Value.PointsDelta);
        Assert.Equal("Customer satisfaction compensation", okValue.Value.Reason);
    }

    [Fact]
    public async Task AdjustPoints_NegativeDelta_DeductsBalance_WhenSufficientFunds()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new AdjustPointsRequest(PointsDelta: -100, Reason: "Point correction due to refund");

        var expectedResponse = new AdjustPointsResponse(
            MemberId: memberId,
            Email: "customer@example.com",
            PointsDelta: -100,
            NewBalance: 400,
            Reason: "Point correction due to refund",
            AdjustedAt: DateTimeOffset.UtcNow
        );

        _mockLoyaltyRepo.Setup(r => r.AdjustMemberPointsAsync(
                memberId, -100, "Point correction due to refund", null, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.AdjustMemberPointsAsync(memberId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<AdjustPointsResponse>>(result);
        Assert.Equal(400, okValue.Value!.NewBalance);
        Assert.Equal(-100, okValue.Value.PointsDelta);
    }

    [Fact]
    public async Task AdjustPoints_RejectsZeroDelta()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new AdjustPointsRequest(PointsDelta: 0, Reason: "Zero points test");

        // Act
        var result = await service.AdjustMemberPointsAsync(memberId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        _mockLoyaltyRepo.Verify(r => r.AdjustMemberPointsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AdjustPoints_RejectsEmptyReason()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new AdjustPointsRequest(PointsDelta: 50, Reason: "   ");

        // Act
        var result = await service.AdjustMemberPointsAsync(memberId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task AdjustPoints_ReturnsBadRequest_WhenDeductionExceedsCurrentBalance()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new AdjustPointsRequest(PointsDelta: -1000, Reason: "Deduct 1000 points");

        _mockLoyaltyRepo.Setup(r => r.AdjustMemberPointsAsync(
                memberId, -1000, "Deduct 1000 points", null, It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Cannot deduct 1000 points. Current balance is 200."));

        // Act
        var result = await service.AdjustMemberPointsAsync(memberId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    // =========================================================================
    // 2. Member Tier Override Tests
    // =========================================================================

    [Fact]
    public async Task OverrideTier_PromotesMember_AndReturnsConfirmation()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("marketing_manager");
        var req = new OverrideTierRequest(TierName: "Platinum", Reason: "VIP Partner executive");

        var expectedResponse = new OverrideTierResponse(
            MemberId: memberId,
            Email: "vip@legendcinema.com",
            PreviousTier: "Bronze",
            NewTier: "Platinum",
            Reason: "VIP Partner executive",
            OverriddenAt: DateTimeOffset.UtcNow
        );

        _mockLoyaltyRepo.Setup(r => r.OverrideMemberTierAsync(
                memberId, "Platinum", "VIP Partner executive", It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.OverrideMemberTierAsync(memberId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<OverrideTierResponse>>(result);
        Assert.Equal("Platinum", okValue.Value!.NewTier);
        Assert.Equal("Bronze", okValue.Value.PreviousTier);
        Assert.Equal("VIP Partner executive", okValue.Value.Reason);
    }

    [Fact]
    public async Task OverrideTier_RejectsInvalidTierName()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("marketing_manager");
        var req = new OverrideTierRequest(TierName: "Diamond", Reason: "Comp tier");

        _mockLoyaltyRepo.Setup(r => r.OverrideMemberTierAsync(
                memberId, "Diamond", "Comp tier", It.IsAny<Guid>()))
            .ThrowsAsync(new ArgumentException("Invalid tier 'Diamond'. Allowed tiers: Bronze, Silver, Gold, Platinum."));

        // Act
        var result = await service.OverrideMemberTierAsync(memberId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task OverrideTier_RejectsEmptyReason()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var memberId = Guid.NewGuid();
        var user = CreateUser("marketing_manager");
        var req = new OverrideTierRequest(TierName: "Gold", Reason: "");

        // Act
        var result = await service.OverrideMemberTierAsync(memberId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    // =========================================================================
    // 3. Customer Segmentation Tests
    // =========================================================================

    [Fact]
    public async Task GetCustomerSegments_CalculatesMemberPercentagesAccurately()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var segments = new List<CustomerSegmentSummary>
        {
            new("all", "All Active Members", "Total members", 1000, 100.0m),
            new("vip", "VIP & High Spenders", "Gold/Platinum", 150, 15.0m),
            new("active", "Active (Last 30 Days)", "Recent activity", 600, 60.0m),
            new("lapsed", "Lapsed (>60 Days Inactive)", "No activity", 250, 25.0m),
            new("new_members", "New Signups (<30 Days)", "Joined recently", 200, 20.0m)
        };

        var expectedResponse = new CustomerSegmentsResponse(
            Segments: segments,
            TotalMembers: 1000,
            GeneratedAt: DateTimeOffset.UtcNow
        );

        _mockLoyaltyRepo.Setup(r => r.GetCustomerSegmentsAsync())
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.GetCustomerSegmentsAsync();

        // Assert
        var okValue = Assert.IsType<Ok<CustomerSegmentsResponse>>(result);
        Assert.Equal(1000, okValue.Value!.TotalMembers);
        Assert.Equal(5, okValue.Value.Segments.Count);
        Assert.Equal(15.0m, okValue.Value.Segments.First(s => s.SegmentCode == "vip").Percentage);
    }

    // =========================================================================
    // 4. Marketing Campaigns & Broadcasts Tests
    // =========================================================================

    [Fact]
    public async Task CreateCampaign_ValidatesDateRange_AndReturnsCreated()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(14);

        var req = new CreateCampaignRequest(
            Name: "Avengers Midnight Premiere",
            Description: "Special 20% concession promo",
            TargetSegment: "vip",
            DiscountCode: "AVENGERS20",
            DiscountValue: 20m,
            StartsAt: start,
            EndsAt: end
        );

        var campaignDto = new AdminCampaignDto(
            CampaignId: Guid.NewGuid(),
            Name: req.Name,
            Description: req.Description,
            TargetSegment: "vip",
            Status: "active",
            DiscountCode: "AVENGERS20",
            DiscountValue: 20m,
            StartsAt: start,
            EndsAt: end,
            CreatedBy: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            VouchersCount: 0
        );

        _mockLoyaltyRepo.Setup(r => r.CreateCampaignAsync(req, It.IsAny<Guid>()))
            .ReturnsAsync(campaignDto);

        // Act
        var result = await service.CreateCampaignAsync(req, user);

        // Assert
        var created = Assert.IsType<Created<AdminCampaignDto>>(result);
        Assert.Equal("Avengers Midnight Premiere", created.Value!.Name);
        Assert.Equal("vip", created.Value.TargetSegment);
    }

    [Fact]
    public async Task CreateCampaign_RejectsEndsAtBeforeStartsAt()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(-1);

        var req = new CreateCampaignRequest(
            Name: "Backwards Campaign",
            Description: "Invalid dates",
            TargetSegment: "all",
            DiscountCode: null,
            DiscountValue: null,
            StartsAt: start,
            EndsAt: end
        );

        // Act
        var result = await service.CreateCampaignAsync(req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task BroadcastCampaign_PreviewMode_DispatchesOnlyToPreviewRecipient()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");
        var req = new BroadcastCampaignRequest(
            CampaignId: Guid.NewGuid(),
            TargetSegment: "all",
            Subject: "Exclusive 50% Weekend Flash Sale",
            MessageBody: "Use code FLASH50 for 50% off tickets!",
            PreviewRecipientEmail: "marketing-lead@cinema.com"
        );

        _mockEmailService.Setup(e => e.SendAsync(It.Is<EmailMessage>(m =>
                m.ToEmail == "marketing-lead@cinema.com" &&
                m.Subject.Contains("[PREVIEW]")
            ), default))
            .ReturnsAsync(new EmailDispatchResult(true, "msg-001", null));

        // Act
        var result = await service.BroadcastCampaignAsync(req, user);

        // Assert
        var okValue = Assert.IsType<Ok<BroadcastCampaignResponse>>(result);
        Assert.True(okValue.Value!.Success);
        Assert.Equal(1, okValue.Value.TotalRecipients);
        Assert.Equal(1, okValue.Value.SentCount);
        Assert.Equal(0, okValue.Value.FailedCount);
        Assert.Equal("marketing-lead@cinema.com", okValue.Value.PreviewRecipient);

        // Ensure bulk recipient fetch is never called
        _mockLoyaltyRepo.Verify(r => r.GetSegmentRecipientEmailsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastCampaign_SegmentMode_DispatchesToAllSegmentMembers()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");
        var req = new BroadcastCampaignRequest(
            CampaignId: Guid.NewGuid(),
            TargetSegment: "vip",
            Subject: "VIP Gold & Platinum Night",
            MessageBody: "Free cocktail on arrival!"
        );

        var emails = new List<string> { "vip1@domain.com", "vip2@domain.com", "vip3@domain.com" };
        _mockLoyaltyRepo.Setup(r => r.GetSegmentRecipientEmailsAsync("vip"))
            .ReturnsAsync(emails);

        _mockEmailService.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), default))
            .ReturnsAsync(new EmailDispatchResult(true, "ok", null));

        // Act
        var result = await service.BroadcastCampaignAsync(req, user);

        // Assert
        var okValue = Assert.IsType<Ok<BroadcastCampaignResponse>>(result);
        Assert.Equal(3, okValue.Value!.TotalRecipients);
        Assert.Equal(3, okValue.Value.SentCount);
        Assert.Equal(0, okValue.Value.FailedCount);

        _mockEmailService.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), default), Times.Exactly(3));
    }

    // =========================================================================
    // 5. Batch Voucher Generation Tests
    // =========================================================================

    [Fact]
    public async Task GenerateVouchersBatch_GeneratesCorrectCountAndPrefix()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");
        var req = new GenerateVouchersBatchRequest(
            CampaignId: Guid.NewGuid(),
            Prefix: "SUMMER2026",
            Count: 5,
            VoucherType: "percentage_discount",
            TargetItemType: "order_total",
            DiscountValue: 15.00m,
            ExpiresAt: DateTimeOffset.UtcNow.AddMonths(2),
            MaxUsesPerCode: 1
        );

        var generatedCodes = new List<string>
        {
            "SUMMER2026-A1B2-1234",
            "SUMMER2026-C3D4-5678",
            "SUMMER2026-E5F6-9012",
            "SUMMER2026-G7H8-3456",
            "SUMMER2026-I9J0-7890"
        };

        var expectedResponse = new GenerateVouchersBatchResponse(
            CampaignId: req.CampaignId,
            GeneratedCount: 5,
            VoucherCodes: generatedCodes,
            VoucherType: "percentage_discount",
            TargetItemType: "order_total",
            DiscountValue: 15.00m,
            ExpiresAt: req.ExpiresAt,
            CreatedAt: DateTimeOffset.UtcNow
        );

        _mockLoyaltyRepo.Setup(r => r.GenerateVouchersBatchAsync(req, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.GenerateVouchersBatchAsync(req, user);

        // Assert
        var okValue = Assert.IsType<Ok<GenerateVouchersBatchResponse>>(result);
        Assert.Equal(5, okValue.Value!.GeneratedCount);
        Assert.All(okValue.Value.VoucherCodes, c => Assert.StartsWith("SUMMER2026", c));
    }

    [Fact]
    public async Task GenerateVouchersBatch_RejectsInvalidCountOrDiscountValue()
    {
        // Arrange
        var service = CreateLoyaltyService();
        var user = CreateUser("marketing_manager");

        // Count 0
        var req1 = new GenerateVouchersBatchRequest(null, "TEST", 0, "fixed_discount", "order_total", 10m, null);
        var res1 = await service.GenerateVouchersBatchAsync(req1, user);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(res1).StatusCode);

        // Discount value <= 0
        var req2 = new GenerateVouchersBatchRequest(null, "TEST", 10, "fixed_discount", "order_total", 0m, null);
        var res2 = await service.GenerateVouchersBatchAsync(req2, user);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(res2).StatusCode);
    }

    // =========================================================================
    // 6. System Feature Flags Tests
    // =========================================================================

    [Fact]
    public async Task GetFeatureFlags_ReturnsConfiguredFlags()
    {
        // Arrange
        var flags = new List<FeatureFlagDto>
        {
            new("enable_google_login", "Enables Google sign in", true, "all", null, DateTimeOffset.UtcNow),
            new("enable_dynamic_surge", "Dynamic surge pricing", true, "all", null, DateTimeOffset.UtcNow),
            new("maintenance_banner", "Maintenance banner", false, "all", null, DateTimeOffset.UtcNow)
        };

        _mockSystemRepo.Setup(r => r.GetFeatureFlagsAsync())
            .ReturnsAsync(flags);

        // Act
        var result = await _mockSystemRepo.Object.GetFeatureFlagsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result.First(f => f.Key == "enable_google_login").IsEnabled);
        Assert.False(result.First(f => f.Key == "maintenance_banner").IsEnabled);
    }

    [Fact]
    public async Task UpdateFeatureFlag_TogglesStateAndAudits()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var updated = new FeatureFlagDto(
            Key: "maintenance_banner",
            Description: "Maintenance banner",
            IsEnabled: true,
            Environment: "all",
            UpdatedBy: actorId,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        _mockSystemRepo.Setup(r => r.UpdateFeatureFlagAsync("maintenance_banner", true, null, null, actorId))
            .ReturnsAsync(updated);

        // Act
        var result = await _mockSystemRepo.Object.UpdateFeatureFlagAsync("maintenance_banner", true, null, null, actorId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsEnabled);
        Assert.Equal(actorId, result.UpdatedBy);
    }

    // =========================================================================
    // 7. System Audit Logs & Integration Health Tests
    // =========================================================================

    [Fact]
    public async Task GetAuditLogs_AppliesPaginationAndFilters()
    {
        // Arrange
        var filter = new AuditLogFilter(ServiceName: "Loyalty.Api", Page: 1, PageSize: 10);
        var entries = new List<SystemAuditLogEntry>
        {
            new(
                LogId: Guid.NewGuid(),
                ActorId: Guid.NewGuid(),
                ActorEmail: "admin@legendcinema.com",
                ActorRole: "super_admin",
                ServiceName: "Loyalty.Api",
                Action: "adjust_points",
                ResourceType: "member",
                ResourceId: Guid.NewGuid().ToString(),
                DetailsJson: "{\"pointsDelta\":200}",
                IpAddress: "127.0.0.1",
                OccurredAt: DateTimeOffset.UtcNow
            )
        };

        var response = new SystemAuditLogResponse(entries, 1, 1, 10, 1);
        _mockSystemRepo.Setup(r => r.GetAuditLogsAsync(filter))
            .ReturnsAsync(response);

        // Act
        var result = await _mockSystemRepo.Object.GetAuditLogsAsync(filter);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("adjust_points", result.Items[0].Action);
        Assert.Equal("Loyalty.Api", result.Items[0].ServiceName);
    }

    [Fact]
    public async Task CheckIntegrationsHealth_ReportsHealthy_WhenDependenciesRespond()
    {
        // Arrange
        _mockSystemRepo.Setup(r => r.CheckPostgresHealthAsync())
            .ReturnsAsync(new IntegrationHealthItem("PostgreSQL Database", "Healthy", 1.25, "OK"));

        _mockSystemRepo.Setup(r => r.CheckRedisHealthAsync())
            .ReturnsAsync(new IntegrationHealthItem("Redis Distributed Cache", "Healthy", 0.85, "OK"));

        // Act
        var pg = await _mockSystemRepo.Object.CheckPostgresHealthAsync();
        var redis = await _mockSystemRepo.Object.CheckRedisHealthAsync();

        // Assert
        Assert.Equal("Healthy", pg.Status);
        Assert.Equal("Healthy", redis.Status);
        Assert.True(pg.LatencyMs > 0);
        Assert.True(redis.LatencyMs > 0);
    }
}
