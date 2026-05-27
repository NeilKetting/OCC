using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Results;

namespace OCC.WpfClient.Services
{
    public class DatabaseStatusService : IDatabaseStatusService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConnectionSettings _connectionSettings;
        private readonly ILogger<DatabaseStatusService> _logger;

        public DatabaseStatusService(
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings,
            ILogger<DatabaseStatusService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _connectionSettings = connectionSettings;
            _logger = logger;
        }

        public async Task<DatabaseStatusResult> CheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_connectionSettings.ApiBaseUrl);

                var response = await client.GetAsync("api/health/db-check", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new DatabaseStatusResult(false, "Offline: API Error", string.Empty);
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var databaseName = ParseDatabaseName(content);
                var environmentSuffix = GetEnvironmentSuffix();
                var statusText = $"Online: {databaseName} {environmentSuffix}".Trim();

                return new DatabaseStatusResult(true, statusText, databaseName.ToUpperInvariant());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Database health check failed.");
                return new DatabaseStatusResult(false, "Offline: Disconnected", string.Empty);
            }
        }

        private static string ParseDatabaseName(string content)
        {
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("databaseName", out var dbProp) ||
                    json.RootElement.TryGetProperty("DatabaseName", out dbProp))
                {
                    return dbProp.GetString() ?? "Unknown";
                }
            }
            catch
            {
                return "Parse Error";
            }

            return "Unknown";
        }

        private string GetEnvironmentSuffix()
        {
            return _connectionSettings.SelectedEnvironment switch
            {
                ConnectionSettings.AppEnvironment.Local => "(Local)",
                ConnectionSettings.AppEnvironment.Test => "(Test)",
                _ => string.Empty
            };
        }
    }
}
