using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class WageService : IWageService
    {
        private readonly ILogger<WageService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public WageService(
            ILogger<WageService> logger,
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings,
            IAuthService authService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<WageRun>> GetWageRunsAsync()
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<WageRun>>(
                    GetFullUrl("api/WageRuns"), _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching wage runs");
                return [];
            }
        }

        public async Task<WageRun?> GetWageRunByIdAsync(Guid id)
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<WageRun>(
                    GetFullUrl($"api/WageRuns/{id}"), _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching wage run {Id}", id);
                return null;
            }
        }

        public async Task<WageRun> GenerateDraftRunAsync(
            DateTime startDate, DateTime endDate,
            string? payType, string? branch,
            decimal totalGasCharge, decimal defaultSupervisorFee,
            decimal companyHousingWashingFee, string? notes = null,
            WageRunType runType = WageRunType.Standard,
            PayFrequency payFrequency = PayFrequency.Fortnightly)
        {
            EnsureAuthorization();
            var request = new WageRun
            {
                StartDate = startDate,
                EndDate = endDate,
                PayType = payType,
                Branch = branch,
                InputTotalGasCharge = totalGasCharge,
                InputDefaultSupervisorFee = defaultSupervisorFee,
                InputCompanyHousingWashingFee = companyHousingWashingFee,
                Notes = notes,
                RunType = runType,
                PayFrequency = payFrequency
            };

            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/WageRuns/draft"), request, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<WageRun>(_options)
                ?? throw new Exception("Failed to deserialize draft response.");
        }

        public async Task<WageSettings> GetWageSettingsAsync()
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<WageSettings>(
                    GetFullUrl("api/WageSettings"), _options) ?? new WageSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching wage settings");
                return new WageSettings();
            }
        }

        public async Task<WageSettings> UpdateWageSettingsAsync(WageSettings settings)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl("api/WageSettings"), settings, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<WageSettings>(_options)
                ?? settings;
        }

        public async Task<WageRun> FinalizeRunAsync(WageRun run)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/WageRuns/finalize"), run, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<WageRun>(_options)
                ?? throw new Exception("Failed to deserialize finalized run response.");
        }

        public async Task DeleteRunAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/WageRuns/{id}"));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<IEnumerable<OCC.Shared.DTOs.BankPaymentDto>> GetBankExportDataAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.GetAsync(GetFullUrl($"api/WageRuns/{id}/bank-export"));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<IEnumerable<OCC.Shared.DTOs.BankPaymentDto>>(_options)
                ?? [];
        }

        public async Task<IEnumerable<OCC.Shared.DTOs.BankPaymentDto>> GetBankExportPreviewAsync(WageRun run)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/WageRuns/bank-export-preview"), run, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<IEnumerable<OCC.Shared.DTOs.BankPaymentDto>>(_options)
                ?? [];
        }
    }
}
