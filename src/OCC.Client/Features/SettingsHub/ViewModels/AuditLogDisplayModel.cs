using OCC.Shared.Models;
using System;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public class AuditLogDisplayModel
    {
        public AuditLog Log { get; }
        public string UserName { get; }
        public string EntityName { get; }

        public AuditLogDisplayModel(AuditLog log, string userName, string entityName)
        {
            Log = log;
            UserName = userName;
            EntityName = entityName;
        }

        // Expose Log properties for easy binding
        public int Id => Log.Id;
        public string Action => Log.Action;
        public string TableName => Log.TableName;
        public string RecordId => Log.RecordId;
        public string? NewValues => Log.NewValues;
        public string? OldValues => Log.OldValues;
        public DateTime Timestamp => Log.Timestamp.ToLocalTime();
        // UserId is replaced by UserName for display
    }
}
