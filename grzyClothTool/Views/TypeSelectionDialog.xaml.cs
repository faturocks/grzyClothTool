using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace grzyClothTool.Views
{
    public partial class TypeSelectionDialog : Window
    {
        public string SelectedType { get; private set; }

        public TypeSelectionDialog(List<string> availableTypes, string title)
        {
            InitializeComponent();
            Title = title;
            
            // Populate the list box with available types
            foreach (var type in availableTypes)
            {
                TypeListBox.Items.Add(type);
            }
            
            // Select the first item by default
            if (TypeListBox.Items.Count > 0)
            {
                TypeListBox.SelectedIndex = 0;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (TypeListBox.SelectedItem != null)
            {
                SelectedType = TypeListBox.SelectedItem.ToString();
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a type.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 