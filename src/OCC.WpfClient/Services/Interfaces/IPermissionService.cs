using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IPermissionService
    {
        bool CanAccess(string route);
        bool IsDev { get; }
    }
}
