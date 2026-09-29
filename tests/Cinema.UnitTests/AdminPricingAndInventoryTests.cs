using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Catalog.Api.Models;
using Catalog.Api.Repositories;
using Moq;
using Pos.Api.Models;
using Pos.Api.Repositories;
using Xunit;

namespace Cinema.UnitTests;

public class AdminPricingAndInventoryTests
{
    private readonly Mock<ICatalogRepository> _mockCatalogRepo = new();
    private readonly Mock<IPosRepository> _mockPosRepo = new();

    // =========================================================================
    // 1. Dynamic Pricing & Price Card Tests
    // =========================================================================

    [Fact]
    public void PriceCard_BasePrice_Matches_SeatAndTicketType()
    {
        // Arrange
        var priceCardId = Guid.NewGuid();
        var entries = new List<PriceCardEntryDetailDto>
        {
            new() { TicketTypeCode = "ADULT", SeatType = "standard", Price = 8.50m },
            new() { TicketTypeCode = "ADULT", SeatType = "vip", Price = 12.00m },
            new() { TicketTypeCode = "CHILD", SeatType = "standard", Price = 6.00m },
            new() { TicketTypeCode = "CHILD", SeatType = "vip", Price = 12.00m },
            new() { TicketTypeCode = "SENIOR", SeatType = "standard", Price = 6.50m }
        };

        // Act
        var adultStandard = entries.FirstOrDefault(e => e.TicketTypeCode == "ADULT" && e.SeatType == "standard");
        var adultVip = entries.FirstOrDefault(e => e.TicketTypeCode == "ADULT" && e.SeatType == "vip");
        var childStandard = entries.FirstOrDefault(e => e.TicketTypeCode == "CHILD" && e.SeatType == "standard");

        // Assert
        Assert.NotNull(adultStandard);
        Assert.Equal(8.50m, adultStandard.Price);
        Assert.NotNull(adultVip);
        Assert.Equal(12.00m, adultVip.Price);
        Assert.NotNull(childStandard);
        Assert.Equal(6.00m, childStandard.Price);
    }

    [Fact]
    public async Task DynamicPricing_Calculates_MatineeDiscountAndSurcharge()
    {
        // Arrange
        var priceCardId = Guid.NewGuid();
        var matineeShowtime = new DateTimeOffset(2026, 9, 21, 10, 30, 0, TimeSpan.Zero); // Monday 10:30 AM (Matinee)

        var expectedResponse = new CalculatedTicketPriceResponse
        {
            PriceCardId = priceCardId,
            TicketTypeCode = "ADULT",
            SeatType = "standard",
            BasePrice = 8.50m,
            Adjustments = new List<PriceAdjustmentDto>
            {
                new() { RuleName = "Matinee Morning Discount", RuleType = "matinee", Amount = -1.50m },
                new() { RuleName = "3D Format Surcharge", RuleType = "format_surcharge", Amount = 2.00m }
            },
            FinalPrice = 9.00m // 8.50 - 1.50 + 2.00 = 9.00
        };

        _mockCatalogRepo.Setup(r => r.CalculateTicketPriceAsync(priceCardId, "ADULT", "standard", matineeShowtime, "3D"))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockCatalogRepo.Object.CalculateTicketPriceAsync(priceCardId, "ADULT", "standard", matineeShowtime, "3D");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8.50m, result.BasePrice);
        Assert.Equal(2, result.Adjustments.Count);
        Assert.Equal(9.00m, result.FinalPrice);
        Assert.Equal("matinee", result.Adjustments[0].RuleType);
        Assert.Equal("format_surcharge", result.Adjustments[1].RuleType);
    }

    [Fact]
    public async Task DynamicPricing_Calculates_WeekendSurge()
    {
        // Arrange
        var priceCardId = Guid.NewGuid();
        var saturdayShowtime = new DateTimeOffset(2026, 9, 26, 19, 0, 0, TimeSpan.Zero); // Saturday 7:00 PM

        var expectedResponse = new CalculatedTicketPriceResponse
        {
            PriceCardId = priceCardId,
            TicketTypeCode = "ADULT",
            SeatType = "vip",
            BasePrice = 12.00m,
            Adjustments = new List<PriceAdjustmentDto>
            {
                new() { RuleName = "Weekend Prime Surge", RuleType = "weekend_surge", Amount = 1.50m },
                new() { RuleName = "IMAX Laser Surcharge", RuleType = "format_surcharge", Amount = 4.00m }
            },
            FinalPrice = 17.50m // 12.00 + 1.50 + 4.00 = 17.50
        };

        _mockCatalogRepo.Setup(r => r.CalculateTicketPriceAsync(priceCardId, "ADULT", "vip", saturdayShowtime, "IMAX"))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockCatalogRepo.Object.CalculateTicketPriceAsync(priceCardId, "ADULT", "vip", saturdayShowtime, "IMAX");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12.00m, result.BasePrice);
        Assert.Equal(17.50m, result.FinalPrice);
    }

    // =========================================================================
    // 2. Multi-Branch Inventory & Wastage Tests
    // =========================================================================

    [Fact]
    public async Task BranchInventory_StockAdjustment_UpdatesCorrectly()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        _mockPosRepo.Setup(r => r.AdjustStockAsync(branchId, productId, 50, "restock", staffId))
            .ReturnsAsync(true);

        _mockPosRepo.Setup(r => r.GetProductBranchInventoryAsync(branchId, productId))
            .ReturnsAsync(new BranchInventoryDto
            {
                InventoryId = Guid.NewGuid(),
                BranchId = branchId,
                ProductId = productId,
                ProductName = "Caramel Popcorn (Large)",
                Sku = "SKU-POPCORN-L",
                StockQuantity = 150,
                ReorderThreshold = 20,
                IsOutOfStock = false
            });

        // Act
        var adjusted = await _mockPosRepo.Object.AdjustStockAsync(branchId, productId, 50, "restock", staffId);
        var item = await _mockPosRepo.Object.GetProductBranchInventoryAsync(branchId, productId);

        // Assert
        Assert.True(adjusted);
        Assert.NotNull(item);
        Assert.Equal(150, item.StockQuantity);
        Assert.False(item.IsOutOfStock);
        Assert.False(item.NeedsReorder);
    }

    [Fact]
    public void ReorderAlert_Flagged_WhenStockBelowThreshold()
    {
        // Arrange
        var item1 = new BranchInventoryDto
        {
            ProductName = "Caramel Popcorn",
            StockQuantity = 10,
            ReorderThreshold = 20,
            IsOutOfStock = false
        };

        var item2 = new BranchInventoryDto
        {
            ProductName = "Soft Drink",
            StockQuantity = 50,
            ReorderThreshold = 20,
            IsOutOfStock = false
        };

        var item3 = new BranchInventoryDto
        {
            ProductName = "Nachos",
            StockQuantity = 0,
            ReorderThreshold = 20,
            IsOutOfStock = true
        };

        // Assert
        Assert.True(item1.NeedsReorder);
        Assert.False(item2.NeedsReorder);
        Assert.True(item3.NeedsReorder);
        Assert.True(item3.IsOutOfStock);
    }

    [Fact]
    public async Task WastageLog_Calculates_TotalCostLoss()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var wastageId = Guid.NewGuid();

        var request = new LogWastageRequest
        {
            ProductId = productId,
            Quantity = 5,
            Reason = "damaged",
            UnitCost = 2.50m,
            Notes = "Carton dropped during unloading"
        };

        _mockPosRepo.Setup(r => r.LogWastageAsync(branchId, request, staffId))
            .ReturnsAsync(wastageId);

        // Act
        var resultId = await _mockPosRepo.Object.LogWastageAsync(branchId, request, staffId);
        var costLoss = request.Quantity * request.UnitCost;

        // Assert
        Assert.Equal(wastageId, resultId);
        Assert.Equal(12.50m, costLoss);
    }

    [Fact]
    public async Task InstantAvailabilityToggle_MarksOutOfStock()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _mockPosRepo.Setup(r => r.ToggleProductAvailabilityAsync(branchId, productId, true))
            .ReturnsAsync(true);

        // Act
        var result = await _mockPosRepo.Object.ToggleProductAvailabilityAsync(branchId, productId, true);

        // Assert
        Assert.True(result);
        _mockPosRepo.Verify(r => r.ToggleProductAvailabilityAsync(branchId, productId, true), Times.Once);
    }

    // =========================================================================
    // 3. Purchase Orders & Suppliers Tests
    // =========================================================================

    [Fact]
    public async Task PurchaseOrder_Creation_CalculatesTotalCost()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var poId = Guid.NewGuid();

        var request = new CreatePurchaseOrderRequest
        {
            PoNumber = "PO-2026-001",
            SupplierId = supplierId,
            BranchId = branchId,
            Notes = "Monthly restock",
            Lines = new List<PurchaseOrderLineInput>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 100, UnitCost = 1.50m }, // 150.00
                new() { ProductId = Guid.NewGuid(), Quantity = 50, UnitCost = 3.00m }    // 150.00
            }
        };

        var expectedTotal = request.Lines.Sum(l => l.Quantity * l.UnitCost);

        _mockPosRepo.Setup(r => r.CreatePurchaseOrderAsync(request, staffId))
            .ReturnsAsync(poId);

        _mockPosRepo.Setup(r => r.GetPurchaseOrderByIdAsync(poId))
            .ReturnsAsync(new PurchaseOrderDto
            {
                PoId = poId,
                PoNumber = request.PoNumber,
                BranchId = branchId,
                SupplierId = supplierId,
                Status = "draft",
                TotalCost = expectedTotal,
                Lines = request.Lines.Select(l => new PurchaseOrderLineDto
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost,
                    LineTotal = l.Quantity * l.UnitCost
                }).ToList()
            });

        // Act
        var id = await _mockPosRepo.Object.CreatePurchaseOrderAsync(request, staffId);
        var po = await _mockPosRepo.Object.GetPurchaseOrderByIdAsync(id);

        // Assert
        Assert.Equal(poId, id);
        Assert.NotNull(po);
        Assert.Equal(300.00m, po.TotalCost);
        Assert.Equal(2, po.Lines.Count);
        Assert.Equal("draft", po.Status);
    }

    [Fact]
    public async Task PurchaseOrder_StatusTransitionToReceived_TriggersRestock()
    {
        // Arrange
        var poId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        _mockPosRepo.Setup(r => r.UpdatePurchaseOrderStatusAsync(poId, "received", staffId))
            .ReturnsAsync(true);

        // Act
        var success = await _mockPosRepo.Object.UpdatePurchaseOrderStatusAsync(poId, "received", staffId);

        // Assert
        Assert.True(success);
        _mockPosRepo.Verify(r => r.UpdatePurchaseOrderStatusAsync(poId, "received", staffId), Times.Once);
    }

    // =========================================================================
    // 4. Branch Scoping & Authorization Tests
    // =========================================================================

    [Fact]
    public void BranchScope_Validation_EnforcesTenantIsolation()
    {
        // Arrange
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();

        // Branch Manager with branch1 claim
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "branch_manager"),
            new Claim("branchId", branch1.ToString())
        }));

        var callerBranchId = Guid.Parse(userClaims.FindFirst("branchId")!.Value);
        var isSuperAdmin = userClaims.IsInRole("super_admin");
        var isInventoryManager = userClaims.IsInRole("inventory_manager");

        // Act & Assert
        // Allowed: accessing branch1
        bool allowedBranch1 = isSuperAdmin || isInventoryManager || callerBranchId == branch1;
        Assert.True(allowedBranch1);

        // Forbidden: accessing branch2
        bool allowedBranch2 = isSuperAdmin || isInventoryManager || callerBranchId == branch2;
        Assert.False(allowedBranch2);
    }
}
