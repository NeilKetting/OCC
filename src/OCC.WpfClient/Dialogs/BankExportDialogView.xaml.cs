using System;
using System.Windows;
using OCC.Shared.Models;
using System.ComponentModel;
using System.Reflection;

namespace OCC.WpfClient.Dialogs
{
    public partial class BankExportDialogView : Window
    {
        public DateTime ActionDate { get; private set; }
        public BankFormat SelectedFormat { get; private set; }

        public BankExportDialogView(int totalPayments, decimal totalAmount, DateTime defaultActionDate)
        {
            InitializeComponent();

            TxtTotalPayments.Text = totalPayments.ToString();
            TxtTotalAmount.Text = totalAmount.ToString("R #,##0.00");
            DpActionDate.SelectedDate = defaultActionDate;

            // Load BankFormat enum items with descriptions
            foreach (BankFormat format in Enum.GetValues(typeof(BankFormat)))
            {
                CbFormat.Items.Add(new FormatItem { Format = format, Description = GetEnumDescription(format) });
            }
            CbFormat.SelectedIndex = 0; // Default Standard CSV

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            ActionDate = DpActionDate.SelectedDate ?? DateTime.Today;
            if (CbFormat.SelectedItem is FormatItem item)
            {
                SelectedFormat = item.Format;
            }
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string GetEnumDescription(Enum value)
        {
            FieldInfo? fi = value.GetType().GetField(value.ToString());
            if (fi != null)
            {
                var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0)
                {
                    return attributes[0].Description;
                }
            }
            return value.ToString();
        }

        private class FormatItem
        {
            public BankFormat Format { get; set; }
            public string Description { get; set; } = string.Empty;

            public override string ToString()
            {
                return Description;
            }
        }
    }
}
