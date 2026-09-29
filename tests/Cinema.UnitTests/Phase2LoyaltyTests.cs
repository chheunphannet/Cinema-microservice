using Microsoft.AspNetCore.Http.HttpResults;
using Loyalty.Api.Models;
using Loyalty.Api.Repositories;
using Loyalty.Api.Services;
using Moq;
using Xunit;
using System.Data;
using Cinema.Foundation.Data;
using Microsoft.AspNetCore.Http;

namespace Cinema.UnitTests;

public class Phase2LoyaltyTests
{
    [Fact]
    public async Task GetMemberProfile_ReturnsOk_WhenMemberExists()
    {
        // Arrange
        var mockRepo = new Mock<ILoyaltyRepository>();
        var mockDbFactory = new Mock<IDbConnectionFactory>();
        var mockConn = new Mock<IDbConnection>();
        
        mockDbFactory.Setup(f => f.CreateReadConnection()).Returns(mockConn.Object);
        
        var expectedProfile = new MemberProfileDto(Guid.NewGuid(), "John", "Doe", "john@example.com", null, "Gold", 1.5m, 1000);
        
        mockRepo.Setup(r => r.GetMemberByEmailAsync("john@example.com", mockConn.Object, null))
                .ReturnsAsync(expectedProfile);

        var service = new LoyaltyService(mockRepo.Object, mockDbFactory.Object);

        // Act
        var result = await service.GetMemberProfileAsync("john@example.com");

        // Assert
        Assert.IsType<Ok<MemberProfileDto>>(result);
        var okResult = (Ok<MemberProfileDto>)result;
        Assert.NotNull(okResult.Value);
        Assert.Equal("Gold", okResult.Value.TierName);
        Assert.Equal(1000, okResult.Value.TotalPointsBalance);
    }
    
    [Fact]
    public async Task ValidateVoucher_ReturnsBadRequest_WhenExpired()
    {
        // Arrange
        var mockRepo = new Mock<ILoyaltyRepository>();
        var mockDbFactory = new Mock<IDbConnectionFactory>();
        var mockConn = new Mock<IDbConnection>();
        
        mockDbFactory.Setup(f => f.CreateReadConnection()).Returns(mockConn.Object);
        
        var expiredVoucher = new VoucherDto(Guid.NewGuid(), "EXPIRED-123", "fixed_discount", "order_total", 5.0m, false, DateTimeOffset.UtcNow.AddDays(-1));
        
        mockRepo.Setup(r => r.GetVoucherByCodeAsync("EXPIRED-123", mockConn.Object, null))
                .ReturnsAsync(expiredVoucher);

        var service = new LoyaltyService(mockRepo.Object, mockDbFactory.Object);

        // Act
        var result = await service.ValidateVoucherAsync(new ValidateVoucherRequest("EXPIRED-123", "order_total"));

        // Assert
        Assert.NotNull(result);
        var badRequestResult = result as IStatusCodeHttpResult;
        Assert.NotNull(badRequestResult);
        Assert.Equal(400, badRequestResult.StatusCode);
    }
}
