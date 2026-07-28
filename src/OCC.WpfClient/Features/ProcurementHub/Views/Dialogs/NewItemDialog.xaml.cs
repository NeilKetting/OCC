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
                new OCC.WpfClient.Dialogs.CustomDialogView("Spell Check", "Spelling issues were found:\n" + errorDetails + "\nPlease right-click the red squiggly underlined words to correct them.", "OK", null, null).ShowDialog();
            }
            else
            {
                new OCC.WpfClient.Dialogs.CustomDialogView("Spell Check", "No spelling errors were found.", "OK", null, null).ShowDialog();
            }
        }
    }
}
