using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IBugReportService
    {
        Task SubmitBugAsync(BugReport report);
        Task<IEnumerable<BugReport>> GetBugReportsAsync(bool includeArchived);
        Task<IEnumerable<BugReport>> SearchSolutionsAsync(string query);
        Task<BugReport?> GetBugReportAsync(Guid id);
        Task AddCommentAsync(Guid bugId, string comment, string? status);
        Task DeleteCommentAsync(Guid commentId);
        Task DeleteBugAsync(Guid bugId);
    }
}
