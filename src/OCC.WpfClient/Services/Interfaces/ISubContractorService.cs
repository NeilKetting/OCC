using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISubContractorService
    {
        Task<IEnumerable<SubContractorSummaryDto>> GetSubContractorSummariesAsync();
        Task<SubContractor?> GetSubContractorAsync(Guid id);
        Task<SubContractor> CreateSubContractorAsync(SubContractor subContractor);
        Task<bool> UpdateSubContractorAsync(SubContractor subContractor);
        Task<bool> DeleteSubContractorAsync(Guid id);
    }
}
