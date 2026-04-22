using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISnagService
    {
        Task<IEnumerable<SnagJob>> GetSnagJobsAsync();
        Task<IEnumerable<SnagJob>> GetProjectSnagJobsAsync(Guid projectId);
        Task<IEnumerable<SnagJob>> GetSubContractorSnagJobsAsync(Guid subContractorId);
        Task<SnagJob?> GetSnagJobAsync(Guid id);
        Task<SnagJob> CreateSnagJobAsync(SnagJob snagJob);
        Task<bool> UpdateSnagJobAsync(SnagJob snagJob);
        Task<bool> DeleteSnagJobAsync(Guid id);
    }
}
