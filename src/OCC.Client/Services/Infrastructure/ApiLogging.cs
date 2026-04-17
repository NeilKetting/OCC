using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace OCC.Client.Services.Infrastructure
{
    public static class ApiLogging
    {
        public static async Task LogFailureAsync(string context, HttpResponseMessage response, string? details = null)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                Log.Error("API FAILURE [{Context}] | {Method} {Url} | Status: {StatusCode} ({Reason}) | Content: {Content} | Details: {Details}",
                    context,
                    response.RequestMessage?.Method,
                    response.RequestMessage?.RequestUri,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    content,
                    details ?? "None");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FAILED TO LOG API FAILURE for context: {Context}", context);
            }
        }

        public static void LogException(string context, Exception ex, string? url = null)
        {
            Log.Error(ex, "API EXCEPTION [{Context}] | URL: {Url}", context, url ?? "Unknown");
        }
    }
}
