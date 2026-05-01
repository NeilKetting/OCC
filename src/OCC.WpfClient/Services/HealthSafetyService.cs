using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class HealthSafetyService : IHealthSafetyService
    {
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _settings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public HealthSafetyService(HttpClient httpClient, ConnectionSettings settings, IAuthService authService)
        {
            _httpClient = httpClient;
            _settings = settings;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _settings.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        // --- Incidents ---
        public async Task<IEnumerable<IncidentSummaryDto>> GetIncidentsAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<IncidentSummaryDto>>(GetFullUrl("api/Incidents"), _options) ?? new List<IncidentSummaryDto>();
        }

        public async Task<IncidentDto?> GetIncidentAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IncidentDto>(GetFullUrl($"api/Incidents/{id}"), _options);
        }

        public async Task<IncidentDto?> CreateIncidentAsync(Incident incident)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/Incidents"), incident, _options);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IncidentDto>(_options);
            }
            return null;
        }

        public async Task<bool> UpdateIncidentAsync(Incident incident)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/Incidents/{incident.Id}"), incident, _options);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteIncidentAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/Incidents/{id}"));
            return response.IsSuccessStatusCode;
        }

        public async Task<IncidentPhotoDto?> UploadIncidentPhotoAsync(IncidentPhoto metadata, System.IO.Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(metadata.IncidentId.ToString()), nameof(IncidentPhoto.IncidentId));
            content.Add(new StringContent(metadata.Description ?? ""), nameof(IncidentPhoto.Description));

            if (fileStream.CanSeek) fileStream.Position = 0;
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync(GetFullUrl("api/Incidents/photos"), content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IncidentPhotoDto>(_options);
            }
            return null;
        }

        public async Task<bool> DeleteIncidentPhotoAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/Incidents/photos/{id}"));
            return response.IsSuccessStatusCode;
        }

        public async Task<IncidentDocumentDto?> UploadIncidentDocumentAsync(Guid incidentId, System.IO.Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(incidentId.ToString()), "IncidentId");

            if (fileStream.CanSeek) fileStream.Position = 0;
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync(GetFullUrl("api/Incidents/documents"), content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IncidentDocumentDto>(_options);
            }
            return null;
        }

        public async Task<bool> DeleteIncidentDocumentAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/Incidents/documents/{id}"));
            return response.IsSuccessStatusCode;
        }

        // --- Audits ---
        public async Task<IEnumerable<AuditSummaryDto>> GetAuditsAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<AuditSummaryDto>>(GetFullUrl("api/HseqAudits"), _options) ?? new List<AuditSummaryDto>();
        }

        public async Task<AuditDto?> GetAuditAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<AuditDto>(GetFullUrl($"api/HseqAudits/{id}"), _options);
        }

        public async Task<AuditDto?> CreateAuditAsync(AuditDto audit)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/HseqAudits"), audit, _options);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AuditDto>(_options);
            }
            return null;
        }

        public async Task<bool> UpdateAuditAsync(AuditDto audit)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/HseqAudits/{audit.Id}"), audit, _options);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAuditAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/HseqAudits/{id}"));
            return response.IsSuccessStatusCode;
        }

        public async Task<IEnumerable<AuditNonComplianceItemDto>> GetAuditDeviationsAsync(Guid auditId)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<AuditNonComplianceItemDto>>(GetFullUrl($"api/HseqAudits/{auditId}/deviations"), _options) ?? new List<AuditNonComplianceItemDto>();
        }

        public async Task<AuditAttachmentDto?> UploadAuditAttachmentAsync(HseqAuditAttachment metadata, System.IO.Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(metadata.AuditId.ToString()), nameof(HseqAuditAttachment.AuditId));
            if (metadata.NonComplianceItemId.HasValue)
                content.Add(new StringContent(metadata.NonComplianceItemId.Value.ToString()), nameof(HseqAuditAttachment.NonComplianceItemId));
            content.Add(new StringContent(metadata.FileName ?? ""), nameof(HseqAuditAttachment.FileName));
            content.Add(new StringContent(metadata.UploadedBy ?? ""), nameof(HseqAuditAttachment.UploadedBy));

            if (fileStream.CanSeek) fileStream.Position = 0;
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync(GetFullUrl("api/HseqAudits/attachments"), content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AuditAttachmentDto>(_options);
            }
            return null;
        }

        public async Task<bool> DeleteAuditAttachmentAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/HseqAudits/attachments/{id}"));
            return response.IsSuccessStatusCode;
        }

        // --- Training ---
        public async Task<IEnumerable<HseqTrainingSummaryDto>> GetTrainingSummariesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<HseqTrainingSummaryDto>>(GetFullUrl("api/HseqTraining/summaries"), _options) ?? new List<HseqTrainingSummaryDto>();
        }

        public async Task<IEnumerable<HseqTrainingRecord>> GetTrainingRecordsAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<HseqTrainingRecord>>(GetFullUrl("api/HseqTraining"), _options) ?? new List<HseqTrainingRecord>();
        }

        public async Task<HseqTrainingRecord?> GetTrainingRecordAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<HseqTrainingRecord>(GetFullUrl($"api/HseqTraining/{id}"), _options);
        }

        public async Task<IEnumerable<HseqTrainingSummaryDto>> GetExpiringTrainingAsync(int days)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<HseqTrainingSummaryDto>>(GetFullUrl($"api/HseqTraining/expiring/{days}"), _options) ?? new List<HseqTrainingSummaryDto>();
        }

        public async Task<HseqTrainingRecord?> CreateTrainingRecordAsync(HseqTrainingRecord record)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/HseqTraining"), record, _options);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<HseqTrainingRecord>(_options);
            }
            return null;
        }

        public async Task<bool> UpdateTrainingRecordAsync(HseqTrainingRecord record)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/HseqTraining/{record.Id}"), record, _options);
            return response.IsSuccessStatusCode;
        }

        public async Task<string?> UploadCertificateAsync(System.IO.Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            try
            {
                using var content = new MultipartFormDataContent();
                if (fileStream.CanSeek) fileStream.Position = 0;
                using var streamContent = new StreamContent(fileStream);
                content.Add(streamContent, "file", fileName);

                var response = await _httpClient.PostAsync(GetFullUrl("api/HseqTraining/upload"), content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_options);
                    if (result.TryGetProperty("url", out var urlProp)) return urlProp.GetString();
                    if (result.TryGetProperty("Url", out var urlPropCase)) return urlPropCase.GetString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload Certificate Exception: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> DeleteTrainingRecordAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/HseqTraining/{id}"));
            return response.IsSuccessStatusCode;
        }

        // --- Documents ---
        public async Task<IEnumerable<HseqDocument>> GetDocumentsAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<HseqDocument>>(GetFullUrl("api/HseqDocuments"), _options) ?? new List<HseqDocument>();
        }

        public async Task<HseqDocument?> UploadDocumentAsync(HseqDocument metadata, System.IO.Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(metadata.Title ?? ""), nameof(HseqDocument.Title));
            content.Add(new StringContent(metadata.Category.ToString()), nameof(HseqDocument.Category));
            content.Add(new StringContent(metadata.UploadedBy ?? ""), nameof(HseqDocument.UploadedBy));
            content.Add(new StringContent(metadata.Version ?? "1.0"), nameof(HseqDocument.Version));

            if (fileStream.CanSeek) fileStream.Position = 0;
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync(GetFullUrl("api/HseqDocuments"), content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<HseqDocument>(_options);
            }
            return null;
        }

        public async Task<bool> DeleteDocumentAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/HseqDocuments/{id}"));
            return response.IsSuccessStatusCode;
        }

        // --- Stats ---
        public async Task<HseqDashboardStats?> GetDashboardStatsAsync()
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<HseqDashboardStats>(GetFullUrl("api/HseqStats/dashboard"), _options);
            }
            catch
            {
                return new HseqDashboardStats();
            }
        }

        public async Task<IEnumerable<HseqSafeHourRecord>> GetPerformanceHistoryAsync(int? year = null)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<HseqSafeHourRecord>>(GetFullUrl($"api/HseqStats/history/{year}"), _options) ?? new List<HseqSafeHourRecord>();
        }
    }
}
