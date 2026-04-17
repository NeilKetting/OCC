using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IBugReportService
    {
        Task SubmitBugAsync(BugReport report);
        Task<IEnumerable<BugReport>> GetBugReportsAsync(bool includeArchived = false);
        Task<IEnumerable<BugReport>> SearchSolutionsAsync(string query);
        Task<BugReport?> GetBugReportAsync(Guid id);
        Task AddCommentAsync(Guid bugId, string comment, string? status);
        Task DeleteCommentAsync(Guid commentId);
        Task MarkAsSolutionAsync(Guid commentId);
        Task DeleteBugAsync(Guid bugId);
    }
}
