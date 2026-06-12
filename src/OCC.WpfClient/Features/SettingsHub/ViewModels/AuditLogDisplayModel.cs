using System;
using System.Collections.Generic;
using System.Text.Json;
using OCC.Shared.Models;

namespace OCC.WpfClient.Features.SettingsHub.ViewModels
{
    public class AuditLogDisplayModel
    {
        public AuditLog Log { get; }
        public string UserName { get; }
        public string EntityName { get; }
        public List<ChangedFieldDisplayModel> ChangedFields { get; }

        public AuditLogDisplayModel(AuditLog log, string userName, string entityName)
        {
            Log = log;
            UserName = userName;
            EntityName = entityName;
            ChangedFields = ParseChangedFields();
        }

        // Expose Log properties for easy binding
        public int Id => Log.Id;
        public string Action => Log.Action;
        public string TableName => Log.TableName;
        public string RecordId => Log.RecordId;
        public string? NewValues => Log.NewValues;
        public string? OldValues => Log.OldValues;
        public DateTime Timestamp => Log.Timestamp.ToLocalTime();

        private List<ChangedFieldDisplayModel> ParseChangedFields()
        {
            var fields = new List<ChangedFieldDisplayModel>();
            var oldDict = new Dictionary<string, string>();
            var newDict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(OldValues))
            {
                try
                {
                    using var doc = JsonDocument.Parse(OldValues);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            oldDict[prop.Name] = prop.Value.ToString();
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(NewValues))
            {
                try
                {
                    using var doc = JsonDocument.Parse(NewValues);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            newDict[prop.Name] = prop.Value.ToString();
                        }
                    }
                }
                catch { }
            }

            // Combine all keys
            var allKeys = new HashSet<string>(oldDict.Keys);
            allKeys.UnionWith(newDict.Keys);

            foreach (var key in allKeys)
            {
                oldDict.TryGetValue(key, out var oldVal);
                newDict.TryGetValue(key, out var newVal);

                // Skip if both are identical (no actual change)
                if (string.Equals(oldVal, newVal, StringComparison.Ordinal)) continue;

                // Make user-friendly strings for displays
                fields.Add(new ChangedFieldDisplayModel
                {
                    PropertyName = key,
                    OldValue = string.IsNullOrEmpty(oldVal) ? "-" : oldVal,
                    NewValue = string.IsNullOrEmpty(newVal) ? "-" : newVal
                });
            }

            return fields;
        }
    }

    public class ChangedFieldDisplayModel
    {
        public string PropertyName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }
}
