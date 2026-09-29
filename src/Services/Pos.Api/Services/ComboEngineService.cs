using Microsoft.Extensions.Caching.Distributed;
using Pos.Api.Models;
using Pos.Api.Repositories;
using System.Text.Json;

namespace Pos.Api.Services;

public interface IComboEngineService
{
    Task<(decimal TotalDiscount, List<OrderLineRequest> UpdatedLines)> ApplyComboDiscountsAsync(List<OrderLineRequest> lines);
}

public class ComboEngineService : IComboEngineService
{
    private readonly IPosRepository _repository;
    private readonly IDistributedCache _cache;

    public ComboEngineService(IPosRepository repository, IDistributedCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private async Task<List<ComboDto>> GetCachedCombosAsync()
    {
        const string cacheKey = "pos:combos:active";
        var cached = await _cache.GetStringAsync(cacheKey);
        
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return JsonSerializer.Deserialize<List<ComboDto>>(cached)!;
        }

        var combos = await _repository.GetActiveCombosAsync();
        
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(combos), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        });

        return combos;
    }

    public async Task<(decimal TotalDiscount, List<OrderLineRequest> UpdatedLines)> ApplyComboDiscountsAsync(List<OrderLineRequest> lines)
    {
        var activeCombos = await GetCachedCombosAsync();
        var updatedLines = lines.ToList();
        
        if (!activeCombos.Any())
            return (0m, updatedLines);

        // Track available quantities of each target ID
        var availableItems = new Dictionary<Guid, int>();
        decimal totalDiscount = 0m;

        foreach (var line in updatedLines)
        {
            if (line.ProductId.HasValue)
            {
                if (!availableItems.ContainsKey(line.ProductId.Value))
                    availableItems[line.ProductId.Value] = 0;
                availableItems[line.ProductId.Value] += line.Quantity;
            }
        }

        var sortedCombos = activeCombos.OrderByDescending(c => c.Price).ToList();

        foreach (var combo in sortedCombos)
        {
            if (!combo.Items.Any()) continue;

            bool canFulfillCombo = true;
            while (canFulfillCombo)
            {
                foreach (var req in combo.Items)
                {
                    if (!availableItems.ContainsKey(req.TargetId) || availableItems[req.TargetId] < req.Quantity)
                    {
                        canFulfillCombo = false;
                        break;
                    }
                }

                if (canFulfillCombo)
                {
                    decimal retailValueOfItems = 0m;
                    var linesToDiscount = new List<(int Index, decimal Proportion)>();

                    // First pass: identify items and calculate total retail value of this combo's parts
                    foreach (var req in combo.Items)
                    {
                        availableItems[req.TargetId] -= req.Quantity;
                        
                        var matchingLineIndex = updatedLines.FindIndex(l => l.ProductId == req.TargetId);
                        if (matchingLineIndex >= 0)
                        {
                            var matchingLine = updatedLines[matchingLineIndex];
                            decimal lineRetailValue = matchingLine.UnitPrice * req.Quantity;
                            retailValueOfItems += lineRetailValue;
                            
                            // If it's a product (concession), we want to discount it. We don't discount tickets.
                            if (req.ItemType == "product")
                            {
                                linesToDiscount.Add((matchingLineIndex, lineRetailValue));
                            }
                        }
                    }

                    if (retailValueOfItems > combo.Price)
                    {
                        decimal comboDiscount = retailValueOfItems - combo.Price;
                        totalDiscount += comboDiscount;

                        // Distribute the discount ONLY across the Food & Beverage (product) lines
                        decimal totalDiscountableValue = linesToDiscount.Sum(l => l.Proportion);
                        if (totalDiscountableValue > 0)
                        {
                            foreach (var ltd in linesToDiscount)
                            {
                                decimal shareOfDiscount = comboDiscount * (ltd.Proportion / totalDiscountableValue);
                                var existingLine = updatedLines[ltd.Index];
                                updatedLines[ltd.Index] = existingLine with 
                                { 
                                    DiscountAmount = existingLine.DiscountAmount + shareOfDiscount 
                                };
                            }
                        }
                    }
                }
            }
        }

        return (totalDiscount, updatedLines);
    }
}
