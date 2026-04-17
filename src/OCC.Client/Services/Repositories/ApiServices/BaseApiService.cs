using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Infrastructure;
using System.Linq;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public abstract class BaseApiService<T> : IRepository<T> where T : class, Shared.Models.IEntity
    {
        protected readonly HttpClient _httpClient;
        protected readonly IAuthService _authService;
        
        /// <summary>
        /// The resource name, e.g. "Projects" or "Users"
        /// </summary>
        protected abstract string ApiEndpoint { get; }

        public BaseApiService(IAuthService authService, HttpClient? httpClient = null)
        {
            _authService = authService;
            _httpClient = httpClient ?? new HttpClient();
        }

        protected string GetFullUrl(string path)
        {
            var baseUrl = ConnectionSettings.Instance.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        protected virtual void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.GetAsync(GetFullUrl($"api/{ApiEndpoint}"));
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<IEnumerable<T>>() ?? Enumerable.Empty<T>();
                }

                await ApiLogging.LogFailureAsync($"GetAll {ApiEndpoint}", response);
                return Enumerable.Empty<T>();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException($"GetAll {ApiEndpoint}", ex, GetFullUrl($"api/{ApiEndpoint}"));
                return Enumerable.Empty<T>();
            }
        }

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            var url = GetFullUrl($"api/{ApiEndpoint}/{id}");
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                await ApiLogging.LogFailureAsync($"GetById {ApiEndpoint}", response);
                return null;
            }
            catch (Exception ex)
            {
                ApiLogging.LogException($"GetById {ApiEndpoint}", ex, url);
                return null;
            }
        }

        public virtual async Task AddAsync(T entity)
        {
            var url = GetFullUrl($"api/{ApiEndpoint}");
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.PostAsJsonAsync(url, entity);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await ApiLogging.LogFailureAsync($"Add {ApiEndpoint}", response, errorContent);
                    throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not HttpRequestException)
            {
                ApiLogging.LogException($"Add {ApiEndpoint}", ex, url);
                throw;
            }
        }

        public virtual async Task UpdateAsync(T entity)
        {
            var url = GetFullUrl($"api/{ApiEndpoint}/{entity.Id}");
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.PutAsJsonAsync(url, entity);
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        throw new OCC.Client.Infrastructure.Exceptions.ConcurrencyException();
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    await ApiLogging.LogFailureAsync($"Update {ApiEndpoint}", response, errorContent);
                    throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not HttpRequestException && ex is not OCC.Client.Infrastructure.Exceptions.ConcurrencyException)
            {
                ApiLogging.LogException($"Update {ApiEndpoint}", ex, url);
                throw;
            }
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var url = GetFullUrl($"api/{ApiEndpoint}/{id}");
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.DeleteAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await ApiLogging.LogFailureAsync($"Delete {ApiEndpoint}", response, errorContent);
                    throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not HttpRequestException)
            {
                ApiLogging.LogException($"Delete {ApiEndpoint}", ex, url);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            // Note: For true efficiency, this should push the query to the API (e.g. OData).
            // For now, valid implementation is to fetch all and filter client-side.
            var allItems = await GetAllAsync();
            return allItems.Where(predicate.Compile());
        }
    }
}
