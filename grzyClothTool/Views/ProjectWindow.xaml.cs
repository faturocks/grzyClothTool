using CodeWalker;
using CodeWalker.GameFiles;
using grzyClothTool.Controls;
using grzyClothTool.Extensions;
using grzyClothTool.Helpers;
using grzyClothTool.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;
using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Input;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Data;
using grzyClothTool.Models.Other;

namespace grzyClothTool.Views
{
    /// <summary>
    /// Interaction logic for Project.xaml
    /// </summary>
    public partial class ProjectWindow : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Addon _addon;
        public Addon Addon
        {
            get { return _addon; }
            set
            {
                if (_addon != value)
                {
                    _addon = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedGenderFilter = "All";
        private string _selectedTypeFilter = "All";

        public ProjectWindow()
        {
            InitializeComponent();

            if(DesignerProperties.GetIsInDesignMode(this))
            {
                Addon = new Addon("design");
                DataContext = this;
                return;
            }

            DataContext = MainWindow.AddonManager;
        }

        private async void Add_DrawableFile(object sender, RoutedEventArgs e)
        {
            var btn = sender as CustomButton;
            var sexBtn = btn.Label.ToString().Equals("male", StringComparison.CurrentCultureIgnoreCase) ? Enums.SexType.male : Enums.SexType.female;
            e.Handled = true;

            OpenFileDialog files = new()
            {
                Title = $"Select drawable files ({btn.Label})",
                Filter = "Drawable files (*.ydd)|*.ydd",
                Multiselect = true
            };

            if (files.ShowDialog() == true)
            {
                ProgressHelper.Start();

                await MainWindow.AddonManager.AddDrawables(files.FileNames, sexBtn);

                ProgressHelper.Stop("Added drawables in {0}", true);
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async void Add_DrawableFolder(object sender, RoutedEventArgs e)
        {
            var btn = sender as CustomButton;
            var sexBtn = btn.Tag.ToString().Equals("male", StringComparison.CurrentCultureIgnoreCase) ? Enums.SexType.male : Enums.SexType.female;
            e.Handled = true;

            FolderBrowserDialog folder = new()
            {
                Description = $"Select a folder containing drawable files ({btn.Tag})",
                UseDescriptionForTitle = true
            };

            if (folder.ShowDialog() == DialogResult.OK)
            {
                ProgressHelper.Start();

                var files = Directory.GetFiles(folder.SelectedPath, "*.ydd", SearchOption.AllDirectories).OrderBy(f => Path.GetFileName(f)).ToArray();
                await MainWindow.AddonManager.AddDrawables(files, sexBtn);

                ProgressHelper.Stop("Added drawables in {0}", true);
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async void Add_DrawableFileAuto(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            OpenFileDialog files = new()
            {
                Title = "Select drawable files (auto-detect gender)",
                Filter = "Drawable files (*.ydd)|*.ydd",
                Multiselect = true
            };

            if (files.ShowDialog() == true)
            {
                ProgressHelper.Start();

                await ProcessFilesWithAutoGenderDetection(files.FileNames);

                ProgressHelper.Stop("Added drawables in {0}", true);
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async void Add_DrawableFolderAuto(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            FolderBrowserDialog folder = new()
            {
                Description = "Select a folder containing drawable files (auto-detect gender)",
                UseDescriptionForTitle = true
            };

            if (folder.ShowDialog() == DialogResult.OK)
            {
                ProgressHelper.Start();

                var files = Directory.GetFiles(folder.SelectedPath, "*.ydd", SearchOption.AllDirectories).OrderBy(f => Path.GetFileName(f)).ToArray();
                await ProcessFilesWithAutoGenderDetection(files);

                ProgressHelper.Stop("Added drawables in {0}", true);
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async Task ProcessFilesWithAutoGenderDetection(string[] filePaths)
        {
            // Group files by detected gender
            var maleFiles = new List<string>();
            var femaleFiles = new List<string>();
            var unknownFiles = new List<string>();

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                
                if (fileName.StartsWith("mp_f", StringComparison.OrdinalIgnoreCase))
                {
                    femaleFiles.Add(filePath);
                }
                else if (fileName.StartsWith("mp_m", StringComparison.OrdinalIgnoreCase))
                {
                    maleFiles.Add(filePath);
                }
                else
                {
                    unknownFiles.Add(filePath);
                }
            }

            // Show warning if there are files with unknown gender
            if (unknownFiles.Count > 0)
            {
                var unknownFileNames = string.Join("\n", unknownFiles.Select(Path.GetFileName));
                var message = $"Could not detect gender for {unknownFiles.Count} file(s):\n\n{unknownFileNames}\n\nThese files will be processed as MALE by default.";
                
                CustomMessageBox.Show(message, "Gender Detection Warning", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                
                // Add unknown files to male list as default
                maleFiles.AddRange(unknownFiles);
            }

            // Process male files
            if (maleFiles.Count > 0)
            {
                await MainWindow.AddonManager.AddDrawables(maleFiles.ToArray(), Enums.SexType.male);
            }

            // Process female files
            if (femaleFiles.Count > 0)
            {
                await MainWindow.AddonManager.AddDrawables(femaleFiles.ToArray(), Enums.SexType.female);
            }
        }

        public void SelectedDrawable_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete || Addon.SelectedDrawables.Count == 0)
            {
                return;
            }

            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Shift:
                    // Shift+Delete was pressed, delete the drawable instantly
                    MainWindow.AddonManager.DeleteDrawables([.. Addon.SelectedDrawables]);
                    break;
                case ModifierKeys.Control:
                    // Ctrl+Delete was pressed, replace the drawable instantly
                    ReplaceDrawables([.. Addon.SelectedDrawables]);
                    break;
                default:
                    // Only Delete was pressed, show the message box
                    Delete_SelectedDrawable(sender, new RoutedEventArgs());
                    break;
            }
        }

        private void Delete_SelectedDrawable(object sender, RoutedEventArgs e)
        {
            var count = Addon.SelectedDrawables.Count;

            if (count == 0)
            {
                CustomMessageBox.Show("No drawable(s) selected", "Delete drawable", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            var message = count == 1
                ? $"Are you sure you want to delete this drawable? ({Addon.SelectedDrawable.Name})"
                : $"Are you sure you want to delete these {count} selected drawables?";

            message += "\nThis will CHANGE NUMBERS of everything after this drawable!\n\nDo you want to replace with reserved slot instead?";

            var result = CustomMessageBox.Show(message, "Delete drawable", CustomMessageBox.CustomMessageBoxButtons.DeleteReplaceCancel);
            if (result == CustomMessageBox.CustomMessageBoxResult.Delete)
            {
                MainWindow.AddonManager.DeleteDrawables([.. Addon.SelectedDrawables]);
            }
            else if (result == CustomMessageBox.CustomMessageBoxResult.Replace)
            {
                ReplaceDrawables([.. Addon.SelectedDrawables]);
            }
        }

        private void ReplaceDrawables(List<GDrawable> drawables)
        {
            foreach(var drawable in drawables)
            {
                var reserved = new GDrawableReserved(drawable.Sex, drawable.IsProp, drawable.TypeNumeric, drawable.Number);

                //replace drawable with reserved in the same place
                Addon.Drawables[Addon.Drawables.IndexOf(drawable)] = reserved;
            }
            SaveHelper.SetUnsavedChanges(true);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var addon = e.AddedItems[0] as Addon;
                int index = int.Parse(addon.Name.ToString().Split(' ')[1]) - 1;

                // as we are modyfing the collection, we need to use try-catch
                try
                {
                    Addon = MainWindow.AddonManager.Addons.ElementAt(index);
                    MainWindow.AddonManager.SelectedAddon = Addon;

                    foreach (var menuItem in MainWindow.AddonManager.MoveMenuItems)
                    {
                        menuItem.IsEnabled = menuItem.Header != addon.Name;
                    }
                    
                    // Apply filters to the newly selected addon
                    ApplyFilters();
                } catch (Exception)  { }
            }
        }

        private void BuildResource_Btn(object sender, RoutedEventArgs e)
        {
            BuildWindow buildWindow = new()
            {
                Owner = Window.GetWindow(this)
            };
            buildWindow.ShowDialog();
        }

        private void RecalculateAddons_Btn(object sender, RoutedEventArgs e)
        {
            var message = "This will recalculate addon separation and redistribute all drawables across addons.\n\n" +
                         "Empty addons will be removed and drawables will be reorganized based on the 128 drawable limit per addon.\n\n" +
                         "Do you want to continue?";

            var result = CustomMessageBox.Show(message, "Recalculate Addon Separation", CustomMessageBox.CustomMessageBoxButtons.YesNo);
            if (result != CustomMessageBox.CustomMessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                RecalculateAddonSeparation();
                CustomMessageBox.Show("Addon separation has been recalculated successfully.", "Recalculate Addon Separation", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                LogHelper.Log("Addon separation recalculated manually by user.", LogType.Info);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error recalculating addon separation: {ex.Message}", "Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                LogHelper.Log($"Error recalculating addon separation: {ex.Message}", LogType.Error);
            }
        }

        private void RecalculateAddonSeparation()
        {
            var addonsToRemove = new List<Addon>();
            
            // Find all empty addons
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                if (addon.Drawables.Count == 0)
                {
                    addonsToRemove.Add(addon);
                }
            }
            
            // Remove empty addons (but keep at least one)
            foreach (var addon in addonsToRemove)
            {
                if (MainWindow.AddonManager.Addons.Count > 1)
                {
                    MainWindow.AddonManager.Addons.Remove(addon);
                }
            }
            
            // Redistribute drawables to ensure proper addon separation based on 128 limit
            var allDrawables = new List<GDrawable>();
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                allDrawables.AddRange(addon.Drawables);
                addon.Drawables.Clear();
            }
            
            // Sort all drawables by type and sex for proper redistribution
            allDrawables = allDrawables.OrderBy(d => d.IsProp)
                                     .ThenBy(d => d.Sex)
                                     .ThenBy(d => d.TypeNumeric)
                                     .ThenBy(d => d.Number)
                                     .ToList();
            
            // Clear all existing addons except the first one
            while (MainWindow.AddonManager.Addons.Count > 1)
            {
                MainWindow.AddonManager.Addons.RemoveAt(MainWindow.AddonManager.Addons.Count - 1);
            }
            
            // Redistribute drawables using the existing AddDrawable logic
            foreach (var drawable in allDrawables)
            {
                MainWindow.AddonManager.AddDrawable(drawable);
                drawable.IsNew = false; // Don't mark as new during redistribution
            }
            
            // Adjust addon names to maintain sequential numbering
            for (int i = 0; i < MainWindow.AddonManager.Addons.Count; i++)
            {
                MainWindow.AddonManager.Addons[i].Name = $"Addon {i + 1}";
            }
            
            // Update move menu items
            MainWindow.AddonManager.MoveMenuItems.Clear();
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                MainWindow.AddonManager.MoveMenuItems.Add(new MoveMenuItem() 
                { 
                    Header = addon.Name, 
                    IsEnabled = true 
                });
            }
            
            SaveHelper.SetUnsavedChanges(true);
        }

        private void Preview_Btn(object sender, RoutedEventArgs e)
        {
            if (CWHelper.CWForm == null || CWHelper.CWForm.IsDisposed)
            {
                CWHelper.CWForm = new CustomPedsForm();
                CWHelper.CWForm.FormClosed += CWForm_FormClosed;
            }

            if (Addon.SelectedDrawable == null)
            {
                CWHelper.CWForm.Show();
                MainWindow.AddonManager.IsPreviewEnabled = true;
                return;
            }

            var ydd = CWHelper.CreateYddFile(Addon.SelectedDrawable);
            CWHelper.CWForm.LoadedDrawables.Add(Addon.SelectedDrawable.Name, ydd.Drawables.First());

            if (Addon.SelectedTexture != null)
            {
                var ytd = CWHelper.CreateYtdFile(Addon.SelectedTexture, Addon.SelectedTexture.DisplayName);
                CWHelper.CWForm.LoadedTextures.Add(ydd.Drawables.First(), ytd.TextureDict);
            }

            CWHelper.SetPedModel(Addon.SelectedDrawable.Sex);

            CWHelper.CWForm.Show();
            MainWindow.AddonManager.IsPreviewEnabled = true;
        }

        private void CWForm_FormClosed(object sender, FormClosedEventArgs e)
        {
                MainWindow.AddonManager.IsPreviewEnabled = false;
        }

        private void SelectedDrawable_Changed(object sender, EventArgs e)
        {
            if (e is not SelectionChangedEventArgs args) return;
            args.Handled = true;

            foreach (GDrawable drawable in args.RemovedItems)
            {
                Addon.SelectedDrawables.Remove(drawable);
            }

            foreach (GDrawable drawable in args.AddedItems)
            {
                Addon.SelectedDrawables.Add(drawable);
                drawable.IsNew = false;
            }

            // Handle the case when a single item is selected
            if (Addon.SelectedDrawables.Count == 1)
            {
                Addon.SelectedDrawable = Addon.SelectedDrawables.First();
                if (Addon.SelectedDrawable.Textures.Count > 0)
                {
                    Addon.SelectedTexture = Addon.SelectedDrawable.Textures.First();
                    SelDrawable.SelectedIndex = 0;
                    SelDrawable.SelectedTextures = [Addon.SelectedTexture];
                }
            }
            else
            {
                Addon.SelectedDrawable = null;
                Addon.SelectedTexture = null;
            }

            if (!MainWindow.AddonManager.IsPreviewEnabled || (Addon.SelectedDrawable == null && Addon.SelectedDrawables.Count == 0)) return;
            CWHelper.SendDrawableUpdateToPreview(e);
        }

        private void SelectedDrawable_Updated(object sender, DrawableUpdatedArgs e)
        {
            if (!Addon.TriggerSelectedDrawableUpdatedEvent ||
                !MainWindow.AddonManager.IsPreviewEnabled ||
                (Addon.SelectedDrawable is null && Addon.SelectedDrawables.Count == 0) ||
                Addon.SelectedDrawables.All(d => d.Textures.Count == 0))
            {
                return;
            }

            CWHelper.SendDrawableUpdateToPreview(e);
        }

        private void SelectedDrawable_TextureChanged(object sender, EventArgs e)
        {
            if (e is not SelectionChangedEventArgs args || args.AddedItems.Count == 0)
            {
                Addon.SelectedTexture = null;
                return;
            }

            args.Handled = true;
            Addon.SelectedTexture = (GTexture)args.AddedItems[0];

            if (!MainWindow.AddonManager.IsPreviewEnabled) return;

            // Check if the drawable exists in LoadedDrawables before accessing it
            if (!CWHelper.CWForm.LoadedDrawables.ContainsKey(Addon.SelectedDrawable.Name))
            {
                // If drawable is not loaded, load it first
                var ydd = CWHelper.CreateYddFile(Addon.SelectedDrawable);
                if (ydd != null && ydd.Drawables.Length > 0)
                {
                    CWHelper.CWForm.LoadedDrawables[Addon.SelectedDrawable.Name] = ydd.Drawables.First();
                }
                else
                {
                    return; // Can't load drawable, skip texture update
                }
            }

            var ytd = CWHelper.CreateYtdFile(Addon.SelectedTexture, Addon.SelectedTexture.DisplayName);
            var cwydd = CWHelper.CWForm.LoadedDrawables[Addon.SelectedDrawable.Name];
            CWHelper.CWForm.LoadedTextures[cwydd] = ytd.TextureDict;
            CWHelper.CWForm.Refresh();
        }

        private void GenderFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedGenderFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                _selectedTypeFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void ApplyFilters()
        {
            if (MainWindow.AddonManager?.SelectedAddon?.Drawables == null) return;

            // Get the current addon's drawables collection
            var drawables = MainWindow.AddonManager.SelectedAddon.Drawables;
            
            // Create or get the CollectionViewSource
            var view = CollectionViewSource.GetDefaultView(drawables);
            
            if (view != null)
            {
                // Remove any existing filter
                view.Filter = null;
                
                // Apply new filter if not "All"
                if (_selectedGenderFilter != "All" || _selectedTypeFilter != "All")
                {
                    view.Filter = item =>
                    {
                        if (item is GDrawable drawable)
                        {
                            // Apply gender filter
                            bool genderMatch = _selectedGenderFilter == "All" ||
                                             (_selectedGenderFilter == "Male" && drawable.Sex == Enums.SexType.male) ||
                                             (_selectedGenderFilter == "Female" && drawable.Sex == Enums.SexType.female);

                            // Apply type filter
                            bool typeMatch = _selectedTypeFilter == "All" ||
                                           (_selectedTypeFilter == "Props" && drawable.IsProp) ||
                                           (_selectedTypeFilter != "Props" && !drawable.IsProp && 
                                            drawable.TypeName?.Equals(_selectedTypeFilter, StringComparison.OrdinalIgnoreCase) == true);

                            return genderMatch && typeMatch;
                        }
                        return true;
                    };
                }
                
                // Refresh the view
                view.Refresh();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
