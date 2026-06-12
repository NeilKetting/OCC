using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public class AuditLogDetailViewModel : OverlayViewModel
    {
        public AuditLogDisplayModel DisplayLog { get; }

        public AuditLogDetailViewModel(AuditLogDisplayModel displayLog)
        {
            DisplayLog = displayLog;
            Title = "Audit Log Details";
        }
    }
}
