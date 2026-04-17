using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiTaskAttachmentService : ITaskAttachmentService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public ApiTaskAttachmentService(HttpClient httpClient, IAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        private void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<TaskAttachment>> GetAttachmentsForTaskAsync(Guid taskId)
        {
            EnsureAuthorization();
            try 
            {
               return await _httpClient.GetFromJsonAsync<IEnumerable<TaskAttachment>>($"api/TaskAttachments/task/{taskId}") 
                      ?? new List<TaskAttachment>();
            }
            catch (Exception)
            {
                return new List<TaskAttachment>();
            }
        }

        public async Task<TaskAttachment?> UploadAttachmentAsync(TaskAttachment metadata, Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            try
            {
                using var content = new MultipartFormDataContent();
                
                // Add metadata fields
                content.Add(new StringContent(metadata.TaskId.ToString()), nameof(TaskAttachment.TaskId));
                content.Add(new StringContent(metadata.UploadedBy ?? ""), nameof(TaskAttachment.UploadedBy));
                
                // Add File
                if (fileStream.CanSeek) fileStream.Position = 0;
                using var streamContent = new StreamContent(fileStream);
                content.Add(streamContent, "file", fileName);

                var response = await _httpClient.PostAsync("api/TaskAttachments/upload", content);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<TaskAttachment>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiTaskAttachmentService] Upload Failed: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> DeleteAttachmentAsync(Guid id)
        {
            EnsureAuthorization();
            try
            {
                var response = await _httpClient.DeleteAsync($"api/TaskAttachments/{id}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
