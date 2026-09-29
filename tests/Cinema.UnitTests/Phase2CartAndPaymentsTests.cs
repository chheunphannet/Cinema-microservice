using Microsoft.AspNetCore.Http.HttpResults;
using Pos.Api.Models;
using Pos.Api.Repositories;
using Pos.Api.Services;
using Moq;
using Xunit;
using System.Data;
using Cinema.Foundation.Data;
using System.Net.Http;

namespace Cinema.UnitTests;

public class Phase2CartAndPaymentsTests
{
    [Fact]
    public async Task ComboEngine_AppliesDiscount_OnlyToFoodAndBeverageLines()
    {
        // Arrange
        var mockRepo = new Mock<IPosRepository>();
        
        var ticketGuid = Guid.NewGuid();
        var popcornGuid = Guid.NewGuid();

        var combos = new List<ComboDto>
        {
            new ComboDto 
            { 
                ComboId = Guid.NewGuid(), 
                Name = "Couples Date Night", 
                Price = 30.0m, 
                Items = new List<ComboItemDto>
                {
                    new ComboItemDto { TargetId = ticketGuid, ItemType = "ticket", Quantity = 2 },
                    new ComboItemDto { TargetId = popcornGuid, ItemType = "product", Quantity = 1 }
                }
            }
        };

        mockRepo.Setup(r => r.GetActiveCombosAsync()).ReturnsAsync(combos);
        
        var mockCache = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        
        mockCache.Setup(c => c.GetAsync("pos:combos:active", It.IsAny<CancellationToken>()))
                 .ReturnsAsync((byte[])null!);
                 
        var service = new ComboEngineService(mockRepo.Object, mockCache.Object);

        var lines = new List<OrderLineRequest>
        {
            new OrderLineRequest { ProductId = ticketGuid, Description = "Standard Ticket", Quantity = 2, UnitPrice = 15.0m },
            new OrderLineRequest { ProductId = popcornGuid, Description = "Large Popcorn", Quantity = 1, UnitPrice = 10.0m }
        };

        // Total retail = $40. Combo price = $30. Expected discount = $10.
        // Studio Split Rule: The $10 discount must be applied ONLY to the popcorn.

        // Act
        var (totalDiscount, updatedLines) = await service.ApplyComboDiscountsAsync(lines);

        // Assert
        Assert.Equal(10.0m, totalDiscount);
        
        // Popcorn should have the full discount applied
        var popcornLine = updatedLines.FirstOrDefault(l => l.Description == "Large Popcorn");
        Assert.NotNull(popcornLine);
        Assert.Equal(10.0m, popcornLine.DiscountAmount);

        // Tickets should have NO discount applied (to protect studio royalties)
        var ticketLine = updatedLines.FirstOrDefault(l => l.Description == "Standard Ticket");
        Assert.NotNull(ticketLine);
        Assert.Equal(0.0m, ticketLine.DiscountAmount);
    }
}
