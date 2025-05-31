using grzyClothTool.Controls;
using grzyClothTool.Helpers;
using grzyClothTool.Models;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Other;
using grzyClothTool.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static grzyClothTool.Controls.CustomMessageBox;
using static grzyClothTool.Enums;

namespace grzyClothTool.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static string GTAVPath => CWHelper.GTAVPath;
        public static bool CacheStartupIsChecked => CWHelper.IsCacheStartupEnabled;

        public static bool IsDarkMode => Properties.Settings.Default.IsDarkMode;

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.AddonManager.Addons.Count > 0)
            {
                MainWindow.NavigationHelper.Navigate("Project");
            }
            else
            {
                MainWindow.NavigationHelper.Navigate("Home");
            }
        }

        private void GTAVPath_Click(object sender, RoutedEventArgs e)
        {
            //get title from e
            var title = e.Source.GetType().GetProperty("Title").GetValue(e.Source).ToString();

            OpenFolderDialog selectedGTAPath = new()
            {
                Title = title,
                Multiselect = false
            };

            if (selectedGTAPath.ShowDialog() == true)
            {
                var exeFilePath = selectedGTAPath.FolderName + "\\GTA5.exe";
                var isPathValid = File.Exists(exeFilePath);

                if (isPathValid)
                {
                    CWHelper.SetGTAFolder(selectedGTAPath.FolderName);
                }
            }
        }

        private void CacheSettings_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxClickEventArgs c = e as CheckBoxClickEventArgs;
            //CWHelper.SetCacheStartup(c.IsChecked);

            LogHelper.Log($"This is not implemented yet :(", LogType.Warning);
        }

        public void PatreonAccount_Click(object sender, RoutedEventArgs e)
        {
            var accountsWindow = new AccountsWindow();
            accountsWindow.ShowDialog();
        }

        public void ThemeModeChange_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.IsChecked.HasValue)
            {
                var value = (bool)toggleButton.IsChecked;
                App.ChangeTheme(value);

                Properties.Settings.Default.IsDarkMode = value;
                Properties.Settings.Default.Save();
            }
        }

        public void DeleteHighPolygonModels_Click(object sender, RoutedEventArgs e)
        {
            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;

            var drawablesToDelete = new List<GDrawable>();

            // Find all drawables with high polygon count in their highest LOD
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                foreach (var drawable in addon.Drawables)
                {
                    // Skip if drawable category is not included
                    if (!ShouldIncludeCategory(drawable, includedCategories))
                        continue;

                    // Check if highest LOD exceeds threshold
                    if (drawable.Details?.AllModels != null)
                    {
                        var highModel = drawable.Details.AllModels[GDrawableDetails.DetailLevel.High];
                        if (highModel != null && highModel.PolyCount > SettingsHelper.Instance.PolygonLimitHigh)
                        {
                            drawablesToDelete.Add(drawable);
                        }
                    }
                }
            }

            if (drawablesToDelete.Count == 0)
            {
                CustomMessageBox.Show("No high polygon models found to delete.", "Delete High Polygon Models", CustomMessageBoxButtons.OKOnly);
                return;
            }

            var message = $"Found {drawablesToDelete.Count} model(s) with polygon count exceeding {SettingsHelper.Instance.PolygonLimitHigh}.\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "Do you want to delete these models?";

            var result = CustomMessageBox.Show(message, "Delete High Polygon Models", CustomMessageBoxButtons.YesNo);
            if (result == CustomMessageBoxResult.Yes)
            {
                MainWindow.AddonManager.DeleteDrawables(drawablesToDelete, true);
                RecalculateAddonSeparation();
                SaveHelper.SetUnsavedChanges(true);
                LogHelper.Log($"Deleted {drawablesToDelete.Count} high polygon model(s).", LogType.Info);
            }
        }

        public void DeleteMissingLOD_Click(object sender, RoutedEventArgs e)
        {
            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;
                
            var drawablesToDelete = new List<GDrawable>();

            // Find all drawables missing Medium and Low LOD
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                foreach (var drawable in addon.Drawables)
                {
                    // Skip if drawable category is not included
                    if (!ShouldIncludeCategory(drawable, includedCategories))
                        continue;

                    if (drawable.Details?.AllModels != null)
                    {
                        var medModel = drawable.Details.AllModels[GDrawableDetails.DetailLevel.Med];
                        var lowModel = drawable.Details.AllModels[GDrawableDetails.DetailLevel.Low];

                        if (medModel == null && lowModel == null)
                        {
                            drawablesToDelete.Add(drawable);
                        }
                    }
                }
            }

            if (drawablesToDelete.Count == 0)
            {
                CustomMessageBox.Show("No models missing Medium and Low LOD found.", "Delete Missing LOD Models", CustomMessageBoxButtons.OKOnly);
                return;
            }

            var message = $"Found {drawablesToDelete.Count} model(s) missing Medium and Low LOD.\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "Do you want to delete these models?";

            var result = CustomMessageBox.Show(message, "Delete Missing LOD Models", CustomMessageBoxButtons.YesNo);
            if (result == CustomMessageBoxResult.Yes)
            {
                MainWindow.AddonManager.DeleteDrawables(drawablesToDelete, true);
                RecalculateAddonSeparation();
                SaveHelper.SetUnsavedChanges(true);
                LogHelper.Log($"Deleted {drawablesToDelete.Count} model(s) missing Medium and Low LOD.", LogType.Info);
            }
        }

        public void DeleteMissingBaseTexture_Click(object sender, RoutedEventArgs e)
        {
            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;
                
            var drawablesToDelete = new List<GDrawable>();

            // Find all drawables with missing base textures
            foreach (var addon in MainWindow.AddonManager.Addons)
            {
                foreach (var drawable in addon.Drawables)
                {
                    // Skip if drawable category is not included
                    if (!ShouldIncludeCategory(drawable, includedCategories))
                        continue;

                    // Skip reserved drawables
                    if (drawable.IsReserved)
                        continue;

                    // Check if drawable has a base texture (not Normal or Specular)
                    bool hasBaseTexture = drawable.Textures.Any(texture => 
                        !IsNormalOrSpecularTexture(texture.DisplayName));

                    if (!hasBaseTexture)
                    {
                        drawablesToDelete.Add(drawable);
                    }
                }
            }

            if (drawablesToDelete.Count == 0)
            {
                CustomMessageBox.Show("No models with missing base textures found.", "Delete Missing Base Texture Models", CustomMessageBoxButtons.OKOnly);
                return;
            }

            var message = $"Found {drawablesToDelete.Count} model(s) without base diffuse textures.\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "These models only have Normal/Specular maps but no base texture.\n\n" +
                         "Do you want to delete these models?";

            var result = CustomMessageBox.Show(message, "Delete Missing Base Texture Models", CustomMessageBoxButtons.YesNo);
            if (result == CustomMessageBoxResult.Yes)
            {
                MainWindow.AddonManager.DeleteDrawables(drawablesToDelete, true);
                RecalculateAddonSeparation();
                SaveHelper.SetUnsavedChanges(true);
                LogHelper.Log($"Deleted {drawablesToDelete.Count} model(s) with missing base textures.", LogType.Info);
            }
        }

        private bool IsNormalOrSpecularTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return false;

            var lowerName = textureName.ToLowerInvariant();
            
            // Check for common normal map suffixes
            if (lowerName.Contains("_n") || lowerName.Contains("_normal") || lowerName.Contains("_nrm") || 
                lowerName.Contains("_bump") || lowerName.Contains("_nm"))
                return true;
                
            // Check for common specular map suffixes  
            if (lowerName.Contains("_s") || lowerName.Contains("_spec") || lowerName.Contains("_specular") ||
                lowerName.Contains("_gloss") || lowerName.Contains("_rough") || lowerName.Contains("_met"))
                return true;
                
            return false;
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
            
            LogHelper.Log($"Recalculated addon separation. Redistributed {allDrawables.Count} drawable(s) across {MainWindow.AddonManager.Addons.Count} addon(s).", LogType.Info);
        }

        private List<string> ShowCategorySelectionDialog()
        {
            var dialog = new CategorySelectionDialog();
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                return dialog.IncludedCategories;
            }
            
            return null; // User cancelled
        }

        private bool ShouldIncludeCategory(GDrawable drawable, List<string> includedCategories)
        {
            if (drawable.IsProp && includedCategories.Contains("props"))
                return true;

            if (!drawable.IsProp && includedCategories.Contains(drawable.TypeName?.ToLower()))
                return true;

            return false;
        }
    }
}
