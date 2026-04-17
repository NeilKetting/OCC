using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using OCC.Client.Services.Interfaces;
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
    public class SupplierImportService : ISupplierImportService
    {
        private readonly ISupplierService _supplierService;
        private readonly ILogger<SupplierImportService> _logger;

        public SupplierImportService(ISupplierService supplierService, ILogger<SupplierImportService> logger)
        {
            _supplierService = supplierService;
            _logger = logger;
        }

        public async Task<(int SuccessCount, int FailureCount, List<string> Errors)> ImportSuppliersAsync(Stream csvStream)
        {
            var successCount = 0;
            var failureCount = 0;
            var errors = new List<string>();

            try
            {
                using var reader = new StreamReader(csvStream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    PrepareHeaderForMatch = args => args.Header.ToLower(),
                });

                var records = csv.GetRecords<SupplierImportRow>();

                var existingSuppliers = await _supplierService.GetSuppliersAsync();
                var existingNames = existingSuppliers.Select(s => s.Name.ToLower().Trim()).ToHashSet();

                foreach (var row in records)
                {
                    if (string.IsNullOrWhiteSpace(row.CompanyName))
                    {
                        continue; // Skip empty rows
                    }

                    if (existingNames.Contains(row.CompanyName.ToLower().Trim()))
                    {
                        errors.Add($"Skipped '{row.CompanyName}' - already exists.");
                        failureCount++;
                        continue;
                    }

                    try
                    {
                        var supplier = MapToSupplier(row);
                        await _supplierService.CreateSupplierAsync(supplier);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import supplier {Name}", row.CompanyName);
                        errors.Add($"Failed to import '{row.CompanyName}': {ex.Message}");
                        failureCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error reading CSV");
                errors.Add($"Fatal error reading CSV: {ex.Message}");
            }

            return (successCount, failureCount, errors);
        }

        private Supplier MapToSupplier(SupplierImportRow row)
        {
            var (cleanAddress, contactPerson) = ParseAddressAndContact(row.StreetAddress);

            return new Supplier
            {
                Id = Guid.NewGuid(), // Or let DB handle it, but shared model sets new Guid
                Name = row.CompanyName.Trim(),
                Address = cleanAddress,
                City = row.City ?? string.Empty,
                PostalCode = row.Zip ?? string.Empty,
                Phone = row.Phone ?? string.Empty,
                Email = row.Email ?? string.Empty,
                ContactPerson = contactPerson,
                // Defaults for missing fields
                VatNumber = string.Empty,
                BankName = string.Empty,
                BankAccountNumber = string.Empty,
                BranchCode = string.Empty,
                SupplierAccountNumber = string.Empty
            };
        }

        private (string Address, string ContactPerson) ParseAddressAndContact(string inputAddress)
        {
            if (string.IsNullOrWhiteSpace(inputAddress))
                return (string.Empty, string.Empty);

            var lines = inputAddress.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var addressLines = new List<string>();
            var contactPerson = string.Empty;

            foreach (var line in lines)
            {
                // Check if line contains "Attn" (case insensitive)
                if (line.Contains("Attn", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract name after "Attn" or "Attn:"
                    // Regex to match "Attn" followed by optional non-word chars like " : "
                    var match = Regex.Match(line, @"attn\s*[:\.]?\s*(.*)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        contactPerson = match.Groups[1].Value.Trim();
                    }
                    else
                    {
                        // Fallback if regex fails but line has Attn
                        contactPerson = line.Replace("Attn", "", StringComparison.OrdinalIgnoreCase).Replace(":", "").Trim();
                    }
                }
                else
                {
                    addressLines.Add(line.Trim());
                }
            }

            var cleanAddress = string.Join(Environment.NewLine, addressLines).Trim();
            return (cleanAddress, contactPerson);
        }

        private class SupplierImportRow
        {
            [Name("Company name")]
            public string CompanyName { get; set; } = string.Empty;

            [Name("Street Address")]
            public string StreetAddress { get; set; } = string.Empty;

            [Name("City")]
            public string City { get; set; } = string.Empty;

            [Name("Zip")]
            public string Zip { get; set; } = string.Empty;
            
            [Name("Phone")]
            public string Phone { get; set; } = string.Empty;

            [Name("Email")]
            public string Email { get; set; } = string.Empty;
        }
    }
}
