using System.Collections.Generic;
using System.Windows;

namespace grzyClothTool.Views
{
    public partial class TextureDeletionDialog : Window
    {
        public List<string> SelectedCriteria { get; private set; }

        public TextureDeletionDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedCriteria = new List<string>();

            if (Delete2048x4096CheckBox.IsChecked == true)
            {
                SelectedCriteria.Add("2048x4096");
            }

            if (Delete1024x2048CheckBox.IsChecked == true)
            {
                SelectedCriteria.Add("1024x2048");
            }

            if (DeletePixelGreater2048CheckBox.IsChecked == true)
            {
                SelectedCriteria.Add("pixel>2048");
            }

            if (DeleteAspectRatio1to2CheckBox.IsChecked == true)
            {
                SelectedCriteria.Add("aspectratio1:2or2:1");
            }

            if (SelectedCriteria.Count == 0)
            {
                MessageBox.Show("Please select at least one criteria.", "No Criteria Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            Delete2048x4096CheckBox.IsChecked = true;
            Delete1024x2048CheckBox.IsChecked = true;
            DeletePixelGreater2048CheckBox.IsChecked = true;
            DeleteAspectRatio1to2CheckBox.IsChecked = true;
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            Delete2048x4096CheckBox.IsChecked = false;
            Delete1024x2048CheckBox.IsChecked = false;
            DeletePixelGreater2048CheckBox.IsChecked = false;
            DeleteAspectRatio1to2CheckBox.IsChecked = false;
        }
    }
} 