using System.Collections.Generic;

namespace OCC.WpfClient.Infrastructure.Models
{
    /// <summary>
    /// Represents configuration options for a single column in a generic list view.
    /// </summary>
    public class ColumnConfig
    {
        public string Header { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public int DisplayIndex { get; set; }
        public double Width { get; set; } = 150;
    }

    /// <summary>
    /// Represents the customized layout configuration of column sets in a generic list view.
    /// </summary>
    public class ListLayout
    {
        public List<ColumnConfig> Columns { get; set; } = new();
    }
}
