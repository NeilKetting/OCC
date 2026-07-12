using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface INoticeBoardService
    {
        Task<IEnumerable<NoticeBoardItem>> GetActiveNoticesAsync();
        Task<NoticeBoardItem> CreateNoticeAsync(NoticeBoardItem item);
        Task<bool> DeleteNoticeAsync(Guid id);
    }
}
