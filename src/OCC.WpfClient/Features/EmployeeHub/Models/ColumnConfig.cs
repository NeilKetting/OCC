using System.Collections.Generic;

namespace OCC.WpfClient.Features.EmployeeHub.Models
{
    public class ColumnConfig
    {
        public string Header { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public int DisplayIndex { get; set; }
        public double Width { get; set; } = 150;
    }

    public class EmployeeListLayout
    {
        public List<ColumnConfig> Columns { get; set; } = new();
    }
}
