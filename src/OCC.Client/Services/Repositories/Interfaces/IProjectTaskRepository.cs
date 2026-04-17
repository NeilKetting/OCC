using OCC.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Repositories.Interfaces
{
    public interface IProjectTaskRepository : IRepository<ProjectTask>
    {
        Task<IEnumerable<ProjectTask>> GetMyTasksAsync();
    }
}
