namespace OCC.WpfClient.Features.Main.Models
{
    public class WidgetConfig
    {
        public string WidgetId { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Column { get; set; }
        public int ColumnSpan { get; set; } = 1;
        public int RowSpan { get; set; } = 1;
        public bool IsVisible { get; set; } = true;
    }
}
