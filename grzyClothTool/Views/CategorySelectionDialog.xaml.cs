using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace grzyClothTool.Views
{
    public partial class CategorySelectionDialog : Window
    {
        public List<string> IncludedCategories { get; private set; }

        public CategorySelectionDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            IncludedCategories = new List<string>();

            foreach (var selectedItem in CategoryListBox.SelectedItems)
            {
                if (selectedItem is ListBoxItem item && item.Content is string content)
                {
                    IncludedCategories.Add(content.ToLower());
                }
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectAll();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.UnselectAll();
        }
    }
} 