using grzyClothTool.Controls;
using grzyClothTool.Helpers;
using grzyClothTool.Models;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Other;
using grzyClothTool.Models.Texture;
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

        private void OffsetConfigPanel_Loaded(object sender, RoutedEventArgs e)
        {
            CreateOffsetControls();
        }

        private void CreateOffsetControls()
        {
            var panel = FindName("OffsetConfigPanel") as StackPanel;
            if (panel == null) return;
            
            // Clear existing controls except the description text
            var description = panel.Children[0];
            panel.Children.Clear();
            panel.Children.Add(description);

            // Add header row
            CreateHeaderRow(panel);

            // Add component types
            var componentTypes = EnumHelper.GetDrawableTypeList();
            foreach (var componentType in componentTypes)
            {
                CreateOffsetControlRow(panel, componentType, false);
            }

            // Add separator
            var separator = new Separator { Margin = new Thickness(0, 10, 0, 10) };
            panel.Children.Add(separator);

            // Add prop types
            var propTypes = EnumHelper.GetPropTypeList();
            foreach (var propType in propTypes)
            {
                CreateOffsetControlRow(panel, propType, true);
            }
        }

        private void CreateHeaderRow(StackPanel parent)
        {
            var headerGrid = new Grid { Margin = new Thickness(0, 5, 0, 2) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Type name
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Male textbox
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Female textbox

            // Type header
            var typeHeader = new TextBlock
            {
                Text = "Type",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(typeHeader, 0);

            // Male header
            var maleHeader = new TextBlock
            {
                Text = "Male",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(maleHeader, 1);

            // Female header
            var femaleHeader = new TextBlock
            {
                Text = "Female",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(femaleHeader, 2);

            headerGrid.Children.Add(typeHeader);
            headerGrid.Children.Add(maleHeader);
            headerGrid.Children.Add(femaleHeader);
            parent.Children.Add(headerGrid);
            
            // Add a separator line under the header
            var headerSeparator = new Separator { Margin = new Thickness(0, 2, 0, 5) };
            parent.Children.Add(headerSeparator);
        }

        private void CreateOffsetControlRow(StackPanel parent, string typeName, bool isProp)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Type name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Male textbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Female textbox

            // Type name label
            var typeLabel = new TextBlock
            {
                Text = $"{(isProp ? "Prop" : "Component")}: {typeName}",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 10, 2)
            };
            Grid.SetColumn(typeLabel, 0);

            // Male textbox
            var maleTextBox = new TextBox
            {
                Text = SettingsHelper.Instance.GetDrawableTypeOffset(typeName, isProp, Enums.SexType.male).ToString(),
                Width = 70,
                Margin = new Thickness(0, 2, 5, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "Male offset"
            };
            Grid.SetColumn(maleTextBox, 1);

            // Female textbox
            var femaleTextBox = new TextBox
            {
                Text = SettingsHelper.Instance.GetDrawableTypeOffset(typeName, isProp, Enums.SexType.female).ToString(),
                Width = 70,
                Margin = new Thickness(5, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "Female offset"
            };
            Grid.SetColumn(femaleTextBox, 2);

            // Handle male textbox changes
            maleTextBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(maleTextBox.Text, out int offset))
                {
                    SettingsHelper.Instance.SetDrawableTypeOffset(typeName, isProp, Enums.SexType.male, offset);
                }
                else
                {
                    // Reset to current value if invalid
                    maleTextBox.Text = SettingsHelper.Instance.GetDrawableTypeOffset(typeName, isProp, Enums.SexType.male).ToString();
                }
            };

            // Handle female textbox changes
            femaleTextBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(femaleTextBox.Text, out int offset))
                {
                    SettingsHelper.Instance.SetDrawableTypeOffset(typeName, isProp, Enums.SexType.female, offset);
                }
                else
                {
                    // Reset to current value if invalid
                    femaleTextBox.Text = SettingsHelper.Instance.GetDrawableTypeOffset(typeName, isProp, Enums.SexType.female).ToString();
                }
            };

            grid.Children.Add(typeLabel);
            grid.Children.Add(maleTextBox);
            grid.Children.Add(femaleTextBox);
            parent.Children.Add(grid);
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

        public void DeleteUnoptimizedTextures_Click(object sender, RoutedEventArgs e)
        {
            // Show texture deletion criteria dialog
            var textureDeletionDialog = new TextureDeletionDialog();
            var result = textureDeletionDialog.ShowDialog();
            
            if (result != true) // User cancelled
                return;

            var selectedCriteria = textureDeletionDialog.SelectedCriteria;

            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;

            var drawablesToDelete = new List<GDrawable>();

            // Find all drawables with textures matching the selected criteria
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

                    // Check if any texture matches the selected criteria
                    bool shouldDelete = false;
                    foreach (var texture in drawable.Textures)
                    {
                        if (texture.TxtDetails != null && DoesTextureMatchCriteria(texture, selectedCriteria))
                        {
                            shouldDelete = true;
                            break;
                        }
                    }

                    if (shouldDelete)
                    {
                        drawablesToDelete.Add(drawable);
                    }
                }
            }

            if (drawablesToDelete.Count == 0)
            {
                CustomMessageBox.Show("No models with textures matching the selected criteria found.", "Delete Unoptimized Textures", CustomMessageBoxButtons.OKOnly);
                return;
            }

            var criteriaText = string.Join(", ", selectedCriteria);
            var message = $"Found {drawablesToDelete.Count} model(s) with textures matching the criteria: {criteriaText}.\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "Do you want to delete these models?";

            var confirmResult = CustomMessageBox.Show(message, "Delete Unoptimized Textures", CustomMessageBoxButtons.YesNo);
            if (confirmResult == CustomMessageBoxResult.Yes)
            {
                MainWindow.AddonManager.DeleteDrawables(drawablesToDelete, true);
                RecalculateAddonSeparation();
                SaveHelper.SetUnsavedChanges(true);
                LogHelper.Log($"Deleted {drawablesToDelete.Count} model(s) with unoptimized textures matching criteria: {criteriaText}.", LogType.Info);
            }
        }

        public void DeleteDuplicatedObjects_Click(object sender, RoutedEventArgs e)
        {
            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;

            var drawablesToDelete = new List<GDrawable>();
            var duplicateGroups = new Dictionary<string, List<GDrawable>>();

            // Collect all drawables from the selected categories
            var allDrawables = new List<GDrawable>();
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

                    allDrawables.Add(drawable);
                }
            }

            if (allDrawables.Count == 0)
            {
                CustomMessageBox.Show("No models found in the selected categories.", "Remove Duplicated Objects", CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Group drawables by their polygon signature
            foreach (var drawable in allDrawables)
            {
                string polygonSignature = GetPolygonSignature(drawable);
                
                // Only consider drawables with valid polygon data as potential duplicates
                if (!string.IsNullOrEmpty(polygonSignature))
                {
                    if (!duplicateGroups.ContainsKey(polygonSignature))
                    {
                        duplicateGroups[polygonSignature] = new List<GDrawable>();
                    }
                    duplicateGroups[polygonSignature].Add(drawable);
                }
            }

            // Find groups with more than one drawable (duplicates)
            var duplicateEntries = duplicateGroups.Where(kvp => kvp.Value.Count > 1).ToList();

            if (duplicateEntries.Count == 0)
            {
                CustomMessageBox.Show("No duplicate models found based on polygon counts.", "Remove Duplicated Objects", CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Collect drawables to delete (keep the first one in each group, delete the rest)
            int duplicateCount = 0;
            foreach (var duplicateGroup in duplicateEntries)
            {
                var drawablesInGroup = duplicateGroup.Value;
                // Keep the first drawable, delete the rest
                for (int i = 1; i < drawablesInGroup.Count; i++)
                {
                    drawablesToDelete.Add(drawablesInGroup[i]);
                    duplicateCount++;
                }
            }

            var message = $"Found {duplicateEntries.Count} group(s) of duplicate models with {duplicateCount} duplicate(s) to remove.\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "Duplicates are identified by matching polygon counts across LOD levels.\n" +
                         "The first model in each group will be kept, others will be deleted.\n\n" +
                         "Do you want to delete these duplicate models?";

            var result = CustomMessageBox.Show(message, "Remove Duplicated Objects", CustomMessageBoxButtons.YesNo);
            if (result == CustomMessageBoxResult.Yes)
            {
                MainWindow.AddonManager.DeleteDrawables(drawablesToDelete, true);
                RecalculateAddonSeparation();
                SaveHelper.SetUnsavedChanges(true);
                LogHelper.Log($"Deleted {duplicateCount} duplicate model(s) from {duplicateEntries.Count} group(s).", LogType.Info);
            }
        }

        private string GetPolygonSignature(GDrawable drawable)
        {
            if (drawable.Details?.AllModels == null)
                return null;

            var highModel = drawable.Details.AllModels.GetValueOrDefault(GDrawableDetails.DetailLevel.High);
            var medModel = drawable.Details.AllModels.GetValueOrDefault(GDrawableDetails.DetailLevel.Med);
            var lowModel = drawable.Details.AllModels.GetValueOrDefault(GDrawableDetails.DetailLevel.Low);

            // Create a signature based on polygon counts for each LOD level
            // Use "null" string for missing LODs to distinguish from 0 polygon models
            var highPolygons = highModel?.PolyCount.ToString() ?? "null";
            var medPolygons = medModel?.PolyCount.ToString() ?? "null";
            var lowPolygons = lowModel?.PolyCount.ToString() ?? "null";

            // Include drawable type and gender in signature to avoid false positives
            return $"{drawable.TypeNumeric}_{drawable.Sex}_{highPolygons}_{medPolygons}_{lowPolygons}";
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

        private bool DoesTextureMatchCriteria(GTexture texture, List<string> criteria)
        {
            var details = texture.TxtDetails;
            if (details == null)
                return false;

            int width = details.Width;
            int height = details.Height;

            foreach (var criterion in criteria)
            {
                switch (criterion)
                {
                    case "2048x4096":
                        if ((width == 2048 && height == 4096) || (width == 4096 && height == 2048))
                            return true;
                        break;

                    case "1024x2048":
                        if ((width == 1024 && height == 2048) || (width == 2048 && height == 1024))
                            return true;
                        break;

                    case "pixel>2048":
                        if (width > 2048 || height > 2048)
                            return true;
                        break;

                    case "aspectratio1:2or2:1":
                        double aspectRatio = (double)width / height;
                        if (Math.Abs(aspectRatio - 0.5) < 0.01 || Math.Abs(aspectRatio - 2.0) < 0.01) // 1:2 or 2:1
                        {
                            // Exclude 1:1 aspect ratio (square textures)
                            if (Math.Abs(aspectRatio - 1.0) >= 0.01)
                                return true;
                        }
                        break;
                }
            }

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
