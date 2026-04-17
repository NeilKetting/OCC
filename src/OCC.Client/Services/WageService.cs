using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class WageService : IWageService
    {
        private readonly HttpClient _client;
        private readonly IAuthService _authService;

        public WageService(IAuthService authService)
        {
            _authService = authService;
            var baseUrl = Infrastructure.ConnectionSettings.Instance.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        private void AddAuthHeader()
        {
             var token = _authService.AuthToken;
             if (!string.IsNullOrEmpty(token))
             {
                 _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
             }
        }

        public async Task<IEnumerable<WageRun>> GetWageRunsAsync()
        {
            AddAuthHeader();
            return await _client.GetFromJsonAsync<IEnumerable<WageRun>>("api/WageRuns") ?? new List<WageRun>();
        }

        public async Task<WageRun?> GetWageRunByIdAsync(Guid id)
        {
            AddAuthHeader();
            try {
                return await _client.GetFromJsonAsync<WageRun>($"api/WageRuns/{id}");
            } catch { return null; }
        }

        public async Task<WageRun> GenerateDraftRunAsync(DateTime startDate, DateTime endDate, string? payType, string? branch, decimal totalGasCharge, decimal defaultSupervisorFee, decimal companyHousingWashingFee, string? notes = null)
        {
            AddAuthHeader();
            var request = new WageRun 
            { 
                StartDate = startDate, 
                EndDate = endDate, 
                PayType = payType, 
                Branch = branch,
                InputTotalGasCharge = totalGasCharge,
                InputDefaultSupervisorFee = defaultSupervisorFee,
                InputCompanyHousingWashingFee = companyHousingWashingFee,
                Notes = notes 
            };
            var response = await _client.PostAsJsonAsync("api/WageRuns/draft", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<WageRun>() ?? throw new Exception("Failed to deserialize response");
        }

        public async Task UpdateDraftLinesAsync(Guid id, IEnumerable<WageRunLine> lines)
        {
            AddAuthHeader();
            var response = await _client.PutAsJsonAsync($"api/WageRuns/draft/{id}/lines", lines);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<WageRun> FinalizeRunAsync(WageRun run)
        {
            AddAuthHeader();
            var response = await _client.PostAsJsonAsync("api/WageRuns/finalize", run);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<WageRun>() ?? throw new Exception("Failed to deserialize response");
        }

        public async Task DeleteRunAsync(Guid id)
        {
             AddAuthHeader();
             var response = await _client.DeleteAsync($"api/WageRuns/{id}");
             if (!response.IsSuccessStatusCode)
             {
                 var error = await response.Content.ReadAsStringAsync();
                 throw new Exception(error);
             }
        }
    }
}
