using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace OCC.WpfClient.Features.ProcurementHub.Views.Dialogs
{
    public partial class NewItemDialog : UserControl
    {
        public NewItemDialog()
        {
            InitializeComponent();
        }

        private void SpellingButton_Click(object sender, RoutedEventArgs e)
        {
            bool hasErrors = false;
            string errorDetails = "";

            if (SpellCheck.GetIsEnabled(SkuTextBox))
            {
                int spellingErrorIndex = SkuTextBox.GetNextSpellingErrorCharacterIndex(0, LogicalDirection.Forward);
                if (spellingErrorIndex != -1)
                {
                    hasErrors = true;
                    errorDetails += "- Spelling issue in Item Name/Number\n";
                }
            }

            if (SpellCheck.GetIsEnabled(DescTextBox))
            {
                int spellingErrorIndex = DescTextBox.GetNextSpellingErrorCharacterIndex(0, LogicalDirection.Forward);
                if (spellingErrorIndex != -1)
                {
                    hasErrors = true;
                    errorDetails += "- Spelling issue in Description\n";
                }
            }

            if (hasErrors)
            {
                MessageBox.Show(
                    "Spelling issues were found:\n" + errorDetails + "\nPlease right-click the red squiggly underlined words to correct them.",
                    "Spell Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    "No spelling errors were found.",
                    "Spell Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
