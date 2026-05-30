using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ICrewDeploymentService
    {
        Task<IEnumerable<SiteDeploymentDto>> GetDeploymentsAsync(Guid? projectId = null, DateTime? date = null);
        Task<SiteDeploymentDto?> CreateDeploymentAsync(CreateSiteDeploymentRequest request);
        Task<bool> CancelDeploymentAsync(Guid id);
        Task<IEnumerable<EmployeeSummaryDto>> GetTodayClockedInAsync();
    }
}
