using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OCC.WpfClient.Dialogs
{
    public partial class AssignProjectDialogView : Window
    {
        public Guid? SelectedProjectId { get; private set; }
        public string? CustomSite { get; private set; }
        public bool IsCancelled { get; private set; } = true;

        public AssignProjectDialogView(List<ProjectSummaryDto> projects)
        {
            InitializeComponent();
            ProjectCombo.ItemsSource = projects;
            if (projects.Count > 0)
            {
                ProjectCombo.SelectedIndex = 0;
            }
        }

        private void ProjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is ProjectSummaryDto selectedProj)
            {
                if (selectedProj.Id == Guid.Empty)
                {
                    CustomSiteTxt.Visibility = Visibility.Visible;
                }
                else
                {
                    CustomSiteTxt.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            DialogResult = false;
            Close();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is ProjectSummaryDto selectedProj)
            {
                if (selectedProj.Name.Contains("-- Please Select"))
                {
                    MessageBox.Show("Please select a project/site or select Other.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (selectedProj.Id == Guid.Empty)
                {
                    if (string.IsNullOrWhiteSpace(CustomSiteTxt.Text))
                    {
                        MessageBox.Show("Please specify the custom site location name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    SelectedProjectId = null;
                    CustomSite = CustomSiteTxt.Text.Trim();
                }
                else
                {
                    SelectedProjectId = selectedProj.Id;
                    CustomSite = null;
                }

                IsCancelled = false;
                DialogResult = true;
                Close();
            }
        }
    }
}
