using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Cinema.Foundation.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Reservation.Api.Models;
using Reservation.Api.Repositories;
using Reservation.Api.Services;
using Xunit;

namespace Cinema.UnitTests;

public class AdminBookingAndRefundTests
{
    private readonly Mock<IAdminBookingRepository> _mockRepo = new();
    private readonly Mock<IEmailService> _mockEmailService = new();
    private readonly Mock<ILogger<AdminBookingService>> _mockLogger = new();
    private readonly IConfiguration _configuration;

    public AdminBookingAndRefundTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Tickets:HmacSecret", "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes" },
            { "Ticket:HmacSecret", "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes" },
            { "Jwt:Secret", "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes" },
            { "Gateway:PublicUrl", "http://localhost:8080" },
            { "Ticket:PublicGatewayUrl", "http://localhost:8080" },
            { "Ticket:LogoText", "Legend" },
            { "Ticket:LogoColor", "#dc2626" },
            { "Ticket:Slogan", "CINEMA" },
            { "Ticket:BackgroundColor", "#fafafa" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private AdminBookingService CreateService(StackExchange.Redis.IConnectionMultiplexer? redis = null)
    {
        return new AdminBookingService(
            _mockRepo.Object,
            _mockEmailService.Object,
            _configuration,
            _mockLogger.Object,
            redis: redis
        );
    }

    private static ClaimsPrincipal CreateUser(string role, Guid? branchId = null, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString())
        };

        if (branchId.HasValue)
        {
            claims.Add(new Claim("branchId", branchId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // =========================================================================
    // 1. Master Booking Search & Pagination Tests
    // =========================================================================

    [Fact]
    public async Task SearchBookings_WithReferenceQuery_FiltersCorrectly()
    {
        // Arrange
        var service = CreateService();
        var user = CreateUser("super_admin");
        var filter = new AdminBookingSearchFilter(Query: "BKG-20260917-ABC123");

        var expectedItems = new List<AdminBookingSearchResultItem>
        {
            new()
            {
                ReservationId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                BookingReference = "BKG-20260917-ABC123",
                BranchId = Guid.NewGuid(),
                BranchName = "Downtown Cinema",
                MovieTitle = "Inception 2",
                AuditoriumName = "Screen 1 IMAX",
                ShowtimeStart = DateTimeOffset.UtcNow.AddDays(1),
                SeatsCount = 2,
                TotalAmount = 24.00m,
                Status = "confirmed",
                CustomerEmail = "customer@example.com",
                CustomerPhone = "+1234567890",
                CreatedAt = DateTimeOffset.UtcNow,
                ConfirmedAt = DateTimeOffset.UtcNow
            }
        };

        var expectedResponse = new AdminBookingSearchResponse(
            Items: expectedItems,
            TotalCount: 1,
            Page: 1,
            PageSize: 20,
            TotalPages: 1
        );

        _mockRepo.Setup(r => r.SearchBookingsAsync(It.Is<AdminBookingSearchFilter>(f => f.Query == "BKG-20260917-ABC123")))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.SearchBookingsAsync(filter, user);

        // Assert
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var okValue = Assert.IsType<Ok<AdminBookingSearchResponse>>(result);
        Assert.Single(okValue.Value!.Items);
        Assert.Equal("BKG-20260917-ABC123", okValue.Value.Items[0].BookingReference);
    }

    [Fact]
    public async Task SearchBookings_Pagination_CalculatesTotalPagesAndSlices()
    {
        // Arrange
        var service = CreateService();
        var user = CreateUser("customer_support");
        var filter = new AdminBookingSearchFilter(Page: 2, PageSize: 10);

        var expectedResponse = new AdminBookingSearchResponse(
            Items: new List<AdminBookingSearchResultItem>(),
            TotalCount: 25,
            Page: 2,
            PageSize: 10,
            TotalPages: 3
        );

        _mockRepo.Setup(r => r.SearchBookingsAsync(It.Is<AdminBookingSearchFilter>(f => f.Page == 2 && f.PageSize == 10)))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.SearchBookingsAsync(filter, user);

        // Assert
        var okValue = Assert.IsType<Ok<AdminBookingSearchResponse>>(result);
        Assert.Equal(25, okValue.Value!.TotalCount);
        Assert.Equal(2, okValue.Value.Page);
        Assert.Equal(10, okValue.Value.PageSize);
        Assert.Equal(3, okValue.Value.TotalPages);
    }

    // =========================================================================
    // 2. Branch Tenant Isolation Tests
    // =========================================================================

    [Fact]
    public async Task BranchManager_CanOnlyAccessAssignedBranch_InSearch()
    {
        // Arrange
        var service = CreateService();
        var branchA = Guid.NewGuid();
        var user = CreateUser("branch_manager", branchId: branchA);
        var filter = new AdminBookingSearchFilter();

        _mockRepo.Setup(r => r.SearchBookingsAsync(It.Is<AdminBookingSearchFilter>(f => f.BranchId == branchA)))
            .ReturnsAsync(new AdminBookingSearchResponse(new List<AdminBookingSearchResultItem>(), 0, 1, 20, 0));

        // Act
        var result = await service.SearchBookingsAsync(filter, user);

        // Assert: Auto-scoped to branchA
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        _mockRepo.Verify(r => r.SearchBookingsAsync(It.Is<AdminBookingSearchFilter>(f => f.BranchId == branchA)), Times.Once);
    }

    [Fact]
    public async Task BranchManager_ForbiddenFromSearchingOtherBranch()
    {
        // Arrange
        var service = CreateService();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var user = CreateUser("branch_manager", branchId: branchA);
        var filter = new AdminBookingSearchFilter(BranchId: branchB);

        // Act
        var result = await service.SearchBookingsAsync(filter, user);

        // Assert: 403 Forbidden
        var problemResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problemResult.StatusCode);
        _mockRepo.Verify(r => r.SearchBookingsAsync(It.IsAny<AdminBookingSearchFilter>()), Times.Never);
    }

    [Fact]
    public async Task BranchManager_ForbiddenFromViewingOtherBranchDetail()
    {
        // Arrange
        var service = CreateService();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var user = CreateUser("branch_manager", branchId: branchA);

        _mockRepo.Setup(r => r.GetBookingSummaryAsync(resId))
            .ReturnsAsync((branchB, "confirmed", Guid.NewGuid()));

        // Act
        var result = await service.GetBookingDetailAsync(resId, user);

        // Assert: 403 Forbidden
        var problemResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problemResult.StatusCode);
        _mockRepo.Verify(r => r.GetBookingDetailAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task BranchManager_ForbiddenFromRefundingOtherBranchBooking()
    {
        // Arrange
        var service = CreateService();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var user = CreateUser("branch_manager", branchId: branchA);
        var req = new RefundBookingRequest();

        _mockRepo.Setup(r => r.GetBookingSummaryAsync(resId))
            .ReturnsAsync((branchB, "confirmed", Guid.NewGuid()));

        // Act
        var result = await service.RefundBookingAsync(resId, req, user);

        // Assert: 403 Forbidden
        var problemResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problemResult.StatusCode);
        _mockRepo.Verify(r => r.ExecuteRefundAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>?>(), It.IsAny<List<Guid>?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SuperAdmin_CustomerSupport_FinanceManager_CanAccessAcrossAllBranches()
    {
        // Arrange
        var service = CreateService();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var filter = new AdminBookingSearchFilter(BranchId: branchB);

        _mockRepo.Setup(r => r.SearchBookingsAsync(It.IsAny<AdminBookingSearchFilter>()))
            .ReturnsAsync(new AdminBookingSearchResponse(new List<AdminBookingSearchResultItem>(), 0, 1, 20, 0));

        // Act & Assert for SuperAdmin
        var superAdmin = CreateUser("super_admin");
        var res1 = await service.SearchBookingsAsync(filter, superAdmin);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(res1).StatusCode);

        // Act & Assert for CustomerSupport
        var support = CreateUser("customer_support");
        var res2 = await service.SearchBookingsAsync(filter, support);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(res2).StatusCode);

        // Act & Assert for FinanceManager
        var finance = CreateUser("finance_manager");
        var res3 = await service.SearchBookingsAsync(filter, finance);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(res3).StatusCode);
    }

    // =========================================================================
    // 3. Atomic Refund Workflow Tests
    // =========================================================================

    [Fact]
    public async Task FullRefund_ReleasesAllSeats_MarksTicketsVoid_AndSetsStatusRefunded()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("super_admin");
        var seat1 = Guid.NewGuid();
        var seat2 = Guid.NewGuid();

        var expectedResponse = new RefundBookingResponse(
            RefundId: Guid.NewGuid(),
            ReservationId: resId,
            OrderId: Guid.NewGuid(),
            RefundAmount: 25.00m,
            TotalRefunded: 25.00m,
            RemainingAmount: 0.00m,
            ReservationStatus: "refunded",
            RefundedSeatIds: new List<Guid> { seat1, seat2 },
            RemainingSeatIds: new List<Guid>(),
            RefundedOrderLineIds: new List<Guid>(),
            RefundedAt: DateTimeOffset.UtcNow,
            AuthorizedBy: Guid.NewGuid()
        );

        _mockRepo.Setup(r => r.ExecuteRefundAsync(
                resId, null, null, null, "customer_request", null, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.RefundBookingAsync(resId, new RefundBookingRequest(), user);

        // Assert
        var okValue = Assert.IsType<Ok<RefundBookingResponse>>(result);
        Assert.Equal(25.00m, okValue.Value!.RefundAmount);
        Assert.Equal("refunded", okValue.Value.ReservationStatus);
        Assert.Equal(2, okValue.Value.RefundedSeatIds.Count);
        Assert.Empty(okValue.Value.RemainingSeatIds);
        Assert.Equal(0.00m, okValue.Value.RemainingAmount);
    }

    [Fact]
    public async Task PartialRefund_ReleasesOnlySpecifiedSeats_SetsPartiallyRefunded()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("finance_manager");
        var seat1 = Guid.NewGuid();
        var seat2 = Guid.NewGuid();

        var expectedResponse = new RefundBookingResponse(
            RefundId: Guid.NewGuid(),
            ReservationId: resId,
            OrderId: Guid.NewGuid(),
            RefundAmount: 12.50m,
            TotalRefunded: 12.50m,
            RemainingAmount: 12.50m,
            ReservationStatus: "partially_refunded",
            RefundedSeatIds: new List<Guid> { seat1 },
            RemainingSeatIds: new List<Guid> { seat2 },
            RefundedOrderLineIds: new List<Guid>(),
            RefundedAt: DateTimeOffset.UtcNow,
            AuthorizedBy: Guid.NewGuid()
        );

        _mockRepo.Setup(r => r.ExecuteRefundAsync(
                resId, It.Is<List<Guid>>(s => s.Count == 1 && s[0] == seat1), null, null, "customer_request", "Customer couldn't make it", It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        var req = new RefundBookingRequest(
            SeatIds: new List<Guid> { seat1 },
            Notes: "Customer couldn't make it"
        );

        // Act
        var result = await service.RefundBookingAsync(resId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<RefundBookingResponse>>(result);
        Assert.Equal(12.50m, okValue.Value!.RefundAmount);
        Assert.Equal("partially_refunded", okValue.Value.ReservationStatus);
        Assert.Single(okValue.Value.RefundedSeatIds);
        Assert.Single(okValue.Value.RemainingSeatIds);
        Assert.Equal(seat2, okValue.Value.RemainingSeatIds[0]);
    }

    [Fact]
    public async Task Refund_RejectsAmountExceedingMaxRefundable()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("super_admin");

        _mockRepo.Setup(r => r.ExecuteRefundAsync(
                resId, null, null, 100.00m, "customer_request", null, It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("Refund amount (100.00) exceeds maximum refundable balance (25.00)."));

        var req = new RefundBookingRequest(RefundAmount: 100.00m);

        // Act
        var result = await service.RefundBookingAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task Refund_RejectsInvalidReasonCode()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("super_admin");

        _mockRepo.Setup(r => r.ExecuteRefundAsync(
                resId, null, null, null, "invalid_reason", null, It.IsAny<Guid>()))
            .ThrowsAsync(new ArgumentException("Invalid refund reason code 'invalid_reason'. Allowed values: customer_request, cancelled_showtime, technical_issue, chargeback, duplicate_booking, other."));

        var req = new RefundBookingRequest(ReasonCode: "invalid_reason");

        // Act
        var result = await service.RefundBookingAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    // =========================================================================
    // 4. Ticket Re-Issuance Tests
    // =========================================================================

    [Fact]
    public async Task ReissueTicket_DispatchesEmail_WithPassesAndCalendarIcs()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var ticketId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        var passes = new List<TicketReissuePassItem>
        {
            new(ticketId, seatId, "A", 5, "Standard", "VALID-HASH-ABC")
        };

        _mockRepo.Setup(r => r.PrepareTicketReissueAsync(resId, false))
            .ReturnsAsync((
                Success: true,
                TicketsCount: 1,
                Passes: passes,
                CustomerEmail: "guest@example.com",
                CustomerName: "John Doe",
                MovieTitle: "Oppenheimer",
                BranchName: "Legend Eden Garden",
                AuditoriumName: "Screen 2",
                ShowtimeStart: DateTimeOffset.UtcNow.AddHours(3),
                TotalAmount: 10.00m,
                BookingRef: "BKG-2026-OPP",
                OrderId: Guid.NewGuid()
            ));

        _mockEmailService.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), default))
            .ReturnsAsync(new EmailDispatchResult(true, "msg-123", null));

        _mockRepo.Setup(r => r.MarkTicketsEmailSentAsync(resId, "guest@example.com"))
            .Returns(Task.CompletedTask);

        var req = new ReissueTicketRequest(RegenerateQrTokens: false);

        // Act
        var result = await service.ReissueTicketAsync(resId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<ReissueTicketResponse>>(result);
        Assert.True(okValue.Value!.Success);
        Assert.Equal("guest@example.com", okValue.Value.RecipientEmail);
        Assert.Equal(1, okValue.Value.TicketsCount);
        Assert.False(okValue.Value.QrTokensRegenerated);

        _mockEmailService.Verify(e => e.SendAsync(It.Is<EmailMessage>(m => 
            m.ToEmail == "guest@example.com" &&
            m.Subject.Contains("Oppenheimer") &&
            m.Attachments != null &&
            m.Attachments.Count == 1 &&
            m.Attachments[0].FileName.EndsWith(".ics")
        ), default), Times.Once);

        _mockRepo.Verify(r => r.MarkTicketsEmailSentAsync(resId, "guest@example.com"), Times.Once);
    }

    [Fact]
    public async Task ReissueTicket_WithRegenerateQrTokens_UpdatesTokensAndPasses()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("super_admin");
        var ticketId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        var passes = new List<TicketReissuePassItem>
        {
            new(ticketId, seatId, "B", 10, "VIP", "NEW-REGENERATED-HASH-XYZ")
        };

        _mockRepo.Setup(r => r.PrepareTicketReissueAsync(resId, true))
            .ReturnsAsync((
                Success: true,
                TicketsCount: 1,
                Passes: passes,
                CustomerEmail: "vip@example.com",
                CustomerName: "VIP Customer",
                MovieTitle: "Dune Part 2",
                BranchName: "Legend City Mall",
                AuditoriumName: "Screen 1 IMAX",
                ShowtimeStart: DateTimeOffset.UtcNow.AddHours(5),
                TotalAmount: 15.00m,
                BookingRef: "BKG-2026-DUNE",
                OrderId: Guid.NewGuid()
            ));

        _mockEmailService.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), default))
            .ReturnsAsync(new EmailDispatchResult(true, "msg-456", null));

        var req = new ReissueTicketRequest(RegenerateQrTokens: true);

        // Act
        var result = await service.ReissueTicketAsync(resId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<ReissueTicketResponse>>(result);
        Assert.True(okValue.Value!.Success);
        Assert.True(okValue.Value.QrTokensRegenerated);
        _mockRepo.Verify(r => r.PrepareTicketReissueAsync(resId, true), Times.Once);
    }

    [Fact]
    public async Task ReissueTicket_RejectsIfNoActiveTicketsFound()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("customer_support");

        _mockRepo.Setup(r => r.PrepareTicketReissueAsync(resId, false))
            .ThrowsAsync(new InvalidOperationException($"No active tickets found for reservation {resId}."));

        var req = new ReissueTicketRequest(RegenerateQrTokens: false);

        // Act
        var result = await service.ReissueTicketAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    // =========================================================================
    // 5. Dispute Flagging Tests
    // =========================================================================

    [Fact]
    public async Task FlagDispute_RecordsChargeback_WithValidStatusAndAmount()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("finance_manager");
        var disputeId = Guid.NewGuid();

        var expectedResponse = new FlagDisputeResponse(
            DisputeId: disputeId,
            ReservationId: resId,
            OrderId: Guid.NewGuid(),
            ProviderDisputeId: "dp_stripe_12345",
            Status: "open",
            Amount: 30.00m,
            EvidenceNotes: "Customer claimed card was stolen",
            CreatedAt: DateTimeOffset.UtcNow
        );

        _mockRepo.Setup(r => r.FlagDisputeAsync(resId, It.Is<FlagDisputeRequest>(d => d.Amount == 30.00m)))
            .ReturnsAsync(expectedResponse);

        var req = new FlagDisputeRequest(
            ProviderDisputeId: "dp_stripe_12345",
            Amount: 30.00m,
            EvidenceNotes: "Customer claimed card was stolen",
            Status: "open"
        );

        // Act
        var result = await service.FlagDisputeAsync(resId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<FlagDisputeResponse>>(result);
        Assert.Equal(disputeId, okValue.Value!.DisputeId);
        Assert.Equal("open", okValue.Value.Status);
        Assert.Equal(30.00m, okValue.Value.Amount);
        Assert.Equal("dp_stripe_12345", okValue.Value.ProviderDisputeId);
    }

    [Fact]
    public async Task FlagDispute_RejectsNegativeAmount()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("super_admin");
        var req = new FlagDisputeRequest(Amount: -10.00m);

        // Act
        var result = await service.FlagDisputeAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        _mockRepo.Verify(r => r.FlagDisputeAsync(It.IsAny<Guid>(), It.IsAny<FlagDisputeRequest>()), Times.Never);
    }

    [Fact]
    public async Task BranchManager_WithoutBranchClaim_ForbiddenFromAllBranchOperations()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var userWithoutBranch = CreateUser("branch_manager", branchId: null);

        _mockRepo.Setup(r => r.GetBookingSummaryAsync(resId))
            .ReturnsAsync((branch, "confirmed", Guid.NewGuid()));

        // Act & Assert 1: Detail
        var detailResult = await service.GetBookingDetailAsync(resId, userWithoutBranch);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(detailResult).StatusCode);

        // Act & Assert 2: Refund
        var refundResult = await service.RefundBookingAsync(resId, new RefundBookingRequest(), userWithoutBranch);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(refundResult).StatusCode);

        // Act & Assert 3: Reissue
        var reissueResult = await service.ReissueTicketAsync(resId, new ReissueTicketRequest(), userWithoutBranch);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(reissueResult).StatusCode);

        // Act & Assert 4: Flag Dispute
        var disputeResult = await service.FlagDisputeAsync(resId, new FlagDisputeRequest(Amount: 10m), userWithoutBranch);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(disputeResult).StatusCode);
    }

    [Fact]
    public async Task BranchManager_WithSnakeCaseBranchIdClaim_AccessGranted()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var branch = Guid.NewGuid();

        // Claims with snake_case "branch_id"
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "branch_manager"),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new("branch_id", branch.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        _mockRepo.Setup(r => r.GetBookingSummaryAsync(resId))
            .ReturnsAsync((branch, "confirmed", Guid.NewGuid()));

        _mockRepo.Setup(r => r.GetBookingDetailAsync(resId))
            .ReturnsAsync(new AdminBookingDetailResponse(
                ReservationId: resId,
                OrderId: Guid.NewGuid(),
                BookingReference: "BKG-123",
                Status: "confirmed",
                CreatedAt: DateTimeOffset.UtcNow,
                ConfirmedAt: DateTimeOffset.UtcNow,
                Branch: new AdminBookingBranchDto(branch, "BR1", "Main Branch", null, "UTC"),
                Auditorium: new AdminBookingAuditoriumDto(Guid.NewGuid(), "Screen 1"),
                Movie: new AdminBookingMovieDto(Guid.NewGuid(), "Test Movie", 120, "Action", "G", null),
                Showtime: new AdminBookingShowtimeDto(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2), 10m),
                Customer: new AdminBookingCustomerDto(null, "Guest", "guest@example.com", null, true),
                Seats: new List<AdminBookingSeatDto>(),
                Tickets: new List<AdminBookingTicketDto>(),
                Concessions: new List<AdminBookingConcessionDto>(),
                Financials: new AdminBookingFinancialsDto(10m, 0m, 10m, 10m, 0m, 10m, new List<AdminBookingPaymentDto>()),
                Refunds: new List<AdminBookingRefundDto>(),
                Disputes: new List<AdminBookingDisputeDto>()
            ));

        // Act
        var result = await service.GetBookingDetailAsync(resId, user);

        // Assert: 200 OK
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task ConcessionOnlyRefund_KeepsReservationConfirmed_AndLeavesSeatsIntact()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("finance_manager");
        var concessionLineId = Guid.NewGuid();

        var expectedResponse = new RefundBookingResponse(
            RefundId: Guid.NewGuid(),
            ReservationId: resId,
            OrderId: Guid.NewGuid(),
            RefundAmount: 8.50m,
            TotalRefunded: 8.50m,
            RemainingAmount: 20.00m,
            ReservationStatus: "confirmed", // Reservation keeps confirmed status because 0 seats were refunded!
            RefundedSeatIds: new List<Guid>(),
            RemainingSeatIds: new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            RefundedOrderLineIds: new List<Guid> { concessionLineId },
            RefundedAt: DateTimeOffset.UtcNow,
            AuthorizedBy: Guid.NewGuid()
        );

        _mockRepo.Setup(r => r.ExecuteRefundAsync(
                resId, null, It.Is<List<Guid>>(l => l.Contains(concessionLineId)), null, "customer_request", null, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        var req = new RefundBookingRequest(
            OrderLineIds: new List<Guid> { concessionLineId }
        );

        // Act
        var result = await service.RefundBookingAsync(resId, req, user);

        // Assert
        var okValue = Assert.IsType<Ok<RefundBookingResponse>>(result);
        Assert.Equal("confirmed", okValue.Value!.ReservationStatus);
        Assert.Empty(okValue.Value.RefundedSeatIds);
        Assert.Single(okValue.Value.RefundedOrderLineIds);
    }

    [Fact]
    public async Task RefundBooking_InvalidatesCorrectCatalogAndBlockbusterRedisKeys()
    {
        // Arrange
        var mockRedis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var mockDb = new Mock<StackExchange.Redis.IDatabase>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);

        var service = CreateService(mockRedis.Object);
        var resId = Guid.NewGuid();
        var showtimeId = Guid.NewGuid();
        var seat1 = Guid.NewGuid();
        var user = CreateUser("super_admin");

        var expectedResponse = new RefundBookingResponse(
            RefundId: Guid.NewGuid(),
            ReservationId: resId,
            OrderId: Guid.NewGuid(),
            RefundAmount: 12.00m,
            TotalRefunded: 12.00m,
            RemainingAmount: 0m,
            ReservationStatus: "refunded",
            RefundedSeatIds: new List<Guid> { seat1 },
            RemainingSeatIds: new List<Guid>(),
            RefundedOrderLineIds: new List<Guid>(),
            RefundedAt: DateTimeOffset.UtcNow,
            AuthorizedBy: Guid.NewGuid(),
            ShowtimeId: showtimeId
        );

        _mockRepo.Setup(r => r.ExecuteRefundAsync(resId, null, null, null, "customer_request", null, It.IsAny<Guid>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await service.RefundBookingAsync(resId, new RefundBookingRequest(), user);

        // Assert
        Assert.IsType<Ok<RefundBookingResponse>>(result);

        // Verify that all cache keys are invalidated in a single batched call (N+1 fix)
        mockDb.Verify(d => d.KeyDeleteAsync(
            It.Is<StackExchange.Redis.RedisKey[]>(keys =>
                keys.Length == 5 &&
                keys.Any(k => k == $"catalog:seat-matrix:{showtimeId}") &&
                keys.Any(k => k == $"blockbuster:high-traffic:seat-matrix:{showtimeId}") &&
                keys.Any(k => k == $"cinema:seats:{showtimeId}") &&
                keys.Any(k => k == $"showtimes:{showtimeId}:seats") &&
                keys.Any(k => k == $"seat-hold:{showtimeId}:{seat1}")
            ),
            It.IsAny<StackExchange.Redis.CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReissueTicket_RejectsInvalidEmailFormat()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("customer_support");
        var req = new ReissueTicketRequest(Email: "not-an-email");

        _mockRepo.Setup(r => r.PrepareTicketReissueAsync(resId, false))
            .ReturnsAsync((
                Success: true,
                TicketsCount: 1,
                Passes: new List<TicketReissuePassItem>(),
                CustomerEmail: "original@example.com",
                CustomerName: "John",
                MovieTitle: "Movie",
                BranchName: "Branch",
                AuditoriumName: "Aud",
                ShowtimeStart: DateTimeOffset.UtcNow,
                TotalAmount: 10m,
                BookingRef: "BKG-1",
                OrderId: Guid.NewGuid()
            ));

        // Act
        var result = await service.ReissueTicketAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task FlagDispute_RejectsInvalidStatus()
    {
        // Arrange
        var service = CreateService();
        var resId = Guid.NewGuid();
        var user = CreateUser("finance_manager");

        _mockRepo.Setup(r => r.FlagDisputeAsync(resId, It.IsAny<FlagDisputeRequest>()))
            .ThrowsAsync(new ArgumentException("Invalid dispute status 'fake_status'. Allowed values: open, under_review, won, lost, accepted."));

        var req = new FlagDisputeRequest(Amount: 20m, Status: "fake_status");

        // Act
        var result = await service.FlagDisputeAsync(resId, req, user);

        // Assert: 400 Bad Request
        var badRequest = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }
}
