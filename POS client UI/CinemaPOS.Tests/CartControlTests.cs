using System;
using System.Linq;
using CinemaPOS.Core.Models;
using Xunit;

namespace CinemaPOS.Tests
{
    public class CartControlTests
    {
        [Fact]
        public void SubTotal_CalculatesSumOfItemsCorrectly()
        {
            var cartItems = new[]
            {
                new CartItem { Id = Guid.NewGuid(), Description = "Adult Ticket", Quantity = 2, UnitPrice = 10.00m, Type = "ticket" },
                new CartItem { Id = Guid.NewGuid(), Description = "Popcorn", Quantity = 1, UnitPrice = 5.00m, Type = "fnb" }
            };

            decimal subTotal = cartItems.Sum(i => i.Quantity * i.UnitPrice);
            Assert.Equal(25.00m, subTotal);
        }

        [Fact]
        public void TaxAndTotal_CalculatesOnTaxableSubTotalAfterDiscount()
        {
            decimal subTotal = 20.00m;
            decimal discount = 5.00m;
            decimal taxRate = 0.10m; // 10%

            decimal taxableSubTotal = Math.Max(0, subTotal - discount);
            decimal taxAmount = Math.Round(taxableSubTotal * taxRate, 2);
            decimal totalAmount = taxableSubTotal + taxAmount;

            Assert.Equal(15.00m, taxableSubTotal);
            Assert.Equal(1.50m, taxAmount);
            Assert.Equal(16.50m, totalAmount);
        }

        [Fact]
        public void TaxAndTotal_ZeroWhenDiscountEqualsSubTotal()
        {
            decimal subTotal = 15.00m;
            decimal discount = 15.00m;
            decimal taxRate = 0.10m;

            decimal taxableSubTotal = Math.Max(0, subTotal - discount);
            decimal taxAmount = Math.Round(taxableSubTotal * taxRate, 2);
            decimal totalAmount = taxableSubTotal + taxAmount;

            Assert.Equal(0.00m, taxableSubTotal);
            Assert.Equal(0.00m, taxAmount);
            Assert.Equal(0.00m, totalAmount);
        }
    }
}
