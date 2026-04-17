using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ILogUploadService
    {
        Task UploadLogsAsync();
        Task<System.Collections.Generic.List<OCC.Shared.Models.LogUploadRequest>> GetLogsAsync();
        Task DeleteLogAsync(System.Guid id);
        Task<System.IO.Stream> DownloadLogAsync(System.Guid id);
    }
}
