using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Mobile.Services
{
    public interface IHseqService
    {
        Task<List<HseqDocument>> GetDocumentsAsync(Guid? projectId = null);
    }

    public class HseqService : IHseqService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _settingsService;
        private readonly IAuthService _authService;

        public HseqService(ILocalSettingsService settingsService, IAuthService authService)
        {
            _settingsService = settingsService;
            _authService = authService;
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<List<HseqDocument>> GetDocumentsAsync(Guid? projectId = null)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/HseqDocuments";
                if (projectId.HasValue)
                {
                    url += $"?projectId={projectId.Value}";
                }

                return await _httpClient.GetFromJsonAsync<List<HseqDocument>>(url) ?? new List<HseqDocument>();
            }
            catch
            {
                return new List<HseqDocument>();
            }
        }
    }
}
