using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces; // Use Repo directly or InventoryService if available?
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Services
{
    public class InventoryImportService : IInventoryImportService
    {
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<InventoryImportService> _logger;

        public InventoryImportService(IInventoryService inventoryService, ILogger<InventoryImportService> logger)
        {
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public async Task<(List<InventoryItem> Items, int FailureCount, List<string> Errors)> ImportInventoryAsync(Stream csvStream)
        {
            var items = new List<InventoryItem>();
            var failureCount = 0;
            var errors = new List<string>();

            try
            {
                using var reader = new StreamReader(csvStream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    PrepareHeaderForMatch = args => args.Header.ToLower().Trim(),
                    TrimOptions = TrimOptions.Trim,
                });

                var records = csv.GetRecords<InventoryImportRow>();
                
                // Get existing items to check for duplicates? Logic "Don't duplicate" requested by user.
                var existingItems = await _inventoryService.GetInventoryAsync();
                var existingDescriptions = existingItems.Select(i => i.Description.ToLower().Trim()).ToHashSet();

                foreach (var row in records)
                {
                    if (string.IsNullOrWhiteSpace(row.Description))
                        continue;

                    if (existingDescriptions.Contains(row.Description.ToLower().Trim()))
                    {
                        errors.Add($"Skipped '{row.Description}' - already exists.");
                        failureCount++;
                        continue;
                    }

                    try
                    {
                        var item = MapToInventoryItem(row);
                        
                        // We verify uniqueness but do not save yet.
                        // The ViewModel will handle interactive validation and saving.
                        items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import item {Name}", row.Description);
                        errors.Add($"Failed '{row.Description}': {ex.Message}");
                        failureCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error reading CSV");
                errors.Add($"Fatal error reading CSV: {ex.Message}");
            }

            return (items, failureCount, errors);
        }

        private InventoryItem MapToInventoryItem(InventoryImportRow row)
        {
            // Extract UOM from Description or Name
            // Searching for pattern like "20kg", "50m", "5l" etc.
            var uom = ExtractUom(row.SalesDescription) ?? ExtractUom(row.Description) ?? "ea";

            // Parse Quantity
            double qty = 0;
            if (double.TryParse(row.QuantityOnHand, NumberStyles.Any, CultureInfo.InvariantCulture, out var q))
            {
                qty = q;
            }

            // Parse Cost
            decimal cost = 0;
            if (decimal.TryParse(row.Cost, NumberStyles.Any, CultureInfo.InvariantCulture, out var c))
            {
                cost = c;
            }

            // Parse Price
            decimal price = 0;
            if (decimal.TryParse(row.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            {
                price = p;
            }

             // Parse Reorder Point
            double reorder = 0;
            if (double.TryParse(row.ReorderPoint, NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
            {
                reorder = r;
            }


            // Mapping Logic per User Instruction: "Product/Service = SKU"

            // 1. SKU: Always use "Product/Service Name"
            var effectiveSku = row.Description?.Trim() ?? string.Empty;
            
            // 2. Product Name: Use "Sales Description". Fallback to SKU if empty.
            var rawDesc = row.SalesDescription?.Trim();
            var effectiveName = !string.IsNullOrWhiteSpace(rawDesc) ? rawDesc : effectiveSku;

            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                Description = effectiveName,
                Category = string.IsNullOrWhiteSpace(row.Category) ? "General" : row.Category.Trim(),
                Supplier = string.Empty, // Not in CSV
                Location = "Warehouse", // Default
                JhbQuantity = qty, // User instruction: "Stock can all go to jhb"
                CptQuantity = 0,
                QuantityOnHand = qty, // Derived total
                JhbReorderPoint = reorder, // Map global reorder point to both/primary branch? User said "Manage independently".
                                           // For import, we assume JHB is the primary or we just duplicate it as a starting point.
                                           // Let's set both to safe-guard.
                CptReorderPoint = reorder,
                UnitOfMeasure = uom,
                Sku = effectiveSku,
                AverageCost = cost,
                Price = price,
                TrackLowStock = true,
                Type = ItemType.StockPart
            };
        }

        private string? ExtractUom(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Regex for Units: number followed by kg, m, l, mm, etc.
            // Or just terms like "roll", "bag", "pack"
            // The user mentioned "m, l, kg".

            // Look for standard units with optional value before it e.g. "20kg" or "50m"
            var match = Regex.Match(input, @"\b(\d+(\.\d+)?)\s*(kg|m|l|ml|mm|g)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Return the unit part, e.g. "kg" or maybe the whole thing "20kg"? 
                // UOM is usually just the unit type "kg", but sometimes "Bag (20kg)". 
                // If the item is "Cement", UOM "bag" is better?
                // But user specifically said "m, l, kg so it is there".
                // Let's grab the unit group.
                return match.Groups[3].Value.ToLower();
            }
            
            // Fallback: check for keywords
            if (input.Contains("roll", StringComparison.OrdinalIgnoreCase)) return "roll";
            if (input.Contains("bag", StringComparison.OrdinalIgnoreCase)) return "bag";
            if (input.Contains("box", StringComparison.OrdinalIgnoreCase)) return "box";
            if (input.Contains("can", StringComparison.OrdinalIgnoreCase)) return "can";
            if (input.Contains("each", StringComparison.OrdinalIgnoreCase)) return "ea";

            return null;
        }

        private class InventoryImportRow
        {
            [Name("Product/Service Name")]
            public string Description { get; set; } = string.Empty;

            [Name("Quantity on hand")]
            public string QuantityOnHand { get; set; } = "0";

            [Name("SKU")]
            public string Sku { get; set; } = string.Empty;

            [Name("Cost")]
            public string Cost { get; set; } = "0";

            [Name("Price")]
            public string Price { get; set; } = "0";

            [Name("Sales Description")]
            public string SalesDescription { get; set; } = string.Empty;

            [Name("Category")]
            public string Category { get; set; } = string.Empty;

             [Name("Reorder Point")]
            public string ReorderPoint { get; set; } = "0";
        }
    }
}
