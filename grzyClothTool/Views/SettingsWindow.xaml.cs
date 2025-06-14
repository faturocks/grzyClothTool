using grzyClothTool.Controls;
using grzyClothTool.Helpers;
using grzyClothTool.Models;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Other;
using grzyClothTool.Models.Texture;
using grzyClothTool.Views;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            MainWindow.NavigationHelper.Navigate("Home");
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
            dialog.Title = "Select Categories for Screenshots";
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

        #region Experimental Features

        private async void TakeNativeScreenshots_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.AddonManager.IsPreviewEnabled)
            {
                CustomMessageBox.Show("Preview window is not open. Please open the 3D preview first.", "Take Native Screenshots", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Enable all DLC levels before taking native screenshots
            EnableAllDlcLevels();

            // Show category selection dialog
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null || includedCategories.Count == 0)
            {
                return; // User cancelled or no categories selected
            }

            try
            {
                // Generate native drawables based on the selected categories and drawable type offsets
                var nativeDrawablesToScreenshot = GenerateNativeDrawables(includedCategories);

                if (nativeDrawablesToScreenshot.Count == 0)
                {
                    CustomMessageBox.Show("No native clothes found for the selected categories based on your offset settings.", "Take Native Screenshots", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                    return;
                }

                // Calculate total screenshots that will be taken (1 texture per native drawable)
                int totalScreenshots = nativeDrawablesToScreenshot.Count;

                var message = $"This will take {totalScreenshots} screenshot(s) of native GTA5 clothes.\n\n" +
                             "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                             "This is an experimental feature that captures the original GTA5 clothes.\n\n" +
                             "Do you want to continue?";

                var result = CustomMessageBox.Show(message, "Take Native Screenshots", CustomMessageBox.CustomMessageBoxButtons.YesNo);
                if (result != CustomMessageBox.CustomMessageBoxResult.Yes)
                    return;

                // Clear all currently loaded drawables to prevent conflicts
                if (CWHelper.CWForm.LoadedDrawables.Count > 0)
                {
                    LogHelper.Log("Clearing all loaded drawables before starting native screenshot process", LogType.Info);
                    
                    // Clear loaded textures first
                    CWHelper.CWForm.LoadedTextures.Clear();
                    
                    // Clear loaded drawables
                    CWHelper.CWForm.LoadedDrawables.Clear();
                    
                    // Refresh the preview to clear any displayed drawable
                    CWHelper.CWForm.Refresh();
                }
                
                // Optimize CodeWalker UI for batch screenshots
                CWHelper.OptimizeCodeWalkerForScreenshots(true);
                
                int successCount = 0;
                int totalCount = 0;
                Enums.SexType? currentGender = null; // Track current gender to detect changes


                foreach (var nativeDrawable in nativeDrawablesToScreenshot)
                {
                    try
                    {
                        totalCount++;

                        // Check if gender has changed and update ped model accordingly
                        if (currentGender != nativeDrawable.Sex)
                        {
                            LogHelper.Log($"Gender change detected: switching from {currentGender?.ToString() ?? "none"} to {nativeDrawable.Sex}", LogType.Info);
                            
                            // Clear any loaded drawables before switching gender to prevent conflicts
                            if (CWHelper.CWForm.LoadedDrawables.Count > 0)
                            {
                                CWHelper.CWForm.LoadedTextures.Clear();
                                CWHelper.CWForm.LoadedDrawables.Clear();
                                CWHelper.CWForm.Refresh();
                            }
                            
                            // Clear alpha mask cache to prevent cross-gender contamination
                            CWHelper.ClearAlphaMaskCache();
                            
                            // Set the appropriate ped model for the new gender
                            CWHelper.SetPedModel(nativeDrawable.Sex);
                            currentGender = nativeDrawable.Sex;
                            
                            // Longer delay to allow the ped model to load properly and stabilize
                            await Task.Delay(500);
                            
                            // Re-optimize CodeWalker after gender switch to ensure proper state
                            CWHelper.OptimizeCodeWalkerForScreenshots(true);
                            
                            // Hide all components using the same method as the Models tab checkboxes


                        }

                        // Load the native drawable using reflection to access SelectedPed
                        var selectedPedField = CWHelper.CWForm.GetType().GetField("SelectedPed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (selectedPedField != null)
                        {
                            var selectedPed = selectedPedField.GetValue(CWHelper.CWForm);
                            // var setComponentMethod = selectedPed.GetType().GetMethod("SetComponentDrawable", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(object) });
                            // if (setComponentMethod != null)
                            // {
                            //     setComponentMethod.Invoke(selectedPed, new object[] { nativeDrawable.TypeNumeric, nativeDrawable.DrawableIndex, 0, nativeDrawable.TextureIndex, CWHelper.CWForm.GameFileCache });
                            // }

                            CWHelper.CWForm.SetNativeComponentDrawable(nativeDrawable.TypeNumeric, nativeDrawable.DrawableIndex, 0, nativeDrawable.TextureIndex);

                             var drawablesProperty = selectedPed.GetType().GetProperty("Drawables");
                                if (drawablesProperty != null)
                                {
                                    var drawables = drawablesProperty.GetValue(selectedPed) as Array;
                                    // lock (CWHelper.CWForm.Renderer.RenderSyncRoot)
                                    {
                                        for (int i = 0; i < 12; i++)
                                        {
                                            if (i == nativeDrawable.TypeNumeric)
                                            {
                                                continue;
                                            }
                                            var drawable = drawables?.GetValue(i) as CodeWalker.GameFiles.DrawableBase;
                                            if (drawable != null)
                                            {
                                                CWHelper.CWForm.Renderer.SelectionDrawableDrawFlags[drawable] = false;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    LogHelper.Log("Drawables property not found", LogType.Error);
                                    await Task.Delay(1000);
                                }

                        }
                        // Auto-focus camera on the component
                        // CWHelper.AutoFocusCamera(nativeDrawable.TypeName);
                        
                        // Brief wait for drawable to load
                        await Task.Delay(1000); //was 100

                        string genderCode = nativeDrawable.Sex == Enums.SexType.male ? "M" : "F";
                        string filename = $"{genderCode}_{nativeDrawable.TypeNumeric}_{nativeDrawable.DrawableIndex:D3}_{nativeDrawable.TextureIndex}.png";

                        // Take screenshot
                        bool success = CWHelper.TakeScreenshot($"{nativeDrawable.TypeName}_{nativeDrawable.DrawableIndex}", filename);
                        // bool success = false;
                        // Always check if file was actually created, regardless of return value
                        bool fileSaved = await WaitForScreenshotFile(filename);
                        if (fileSaved)
                        {
                            successCount++;
                            LogHelper.Log($"Native screenshot file successfully saved: {filename}", LogType.Info);
                        }
                        else
                        {
                            LogHelper.Log($"Native screenshot file {filename} was not saved within timeout period", LogType.Warning);
                        }

                        // Brief delay between captures
                        await Task.Delay(250); //was 25
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Log($"Error processing native drawable: {ex.Message}", LogType.Error);
                    }
                }

                CustomMessageBox.Show($"Successfully captured {successCount} out of {totalCount} native screenshot(s).", 
                    "Native Screenshots Complete", CustomMessageBox.CustomMessageBoxButtons.OKOnly);

                LogHelper.Log($"Native screenshot process completed: {successCount}/{totalCount} successful", LogType.Info);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error during native screenshot process: {ex.Message}", "Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                LogHelper.Log($"Native screenshot error: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// Enables all DLC levels in CodeWalker to ensure native drawables beyond index 15 are available
        /// </summary>
        private void EnableAllDlcLevels()
        {
            try
            {
                if (CWHelper.CWForm?.GameFileCache == null)
                {
                    LogHelper.Log("GameFileCache is null, cannot enable DLC levels", LogType.Warning);
                    return;
                }

                var gameFileCache = CWHelper.CWForm.GameFileCache;
                
                // Check if DLC is already enabled and set to the latest version
                if (gameFileCache.EnableDlc && !string.IsNullOrEmpty(gameFileCache.SelectedDlc))
                {
                    // Check if we're already using the latest DLC
                    if (gameFileCache.DlcNameList != null && gameFileCache.DlcNameList.Count > 0)
                    {
                        var latestDlc = gameFileCache.DlcNameList[gameFileCache.DlcNameList.Count - 1];
                        if (gameFileCache.SelectedDlc == latestDlc)
                        {
                            LogHelper.Log($"DLC already enabled with latest version: {latestDlc}", LogType.Info);
                            return;
                        }
                    }
                }

                LogHelper.Log("Enabling all DLC levels in CodeWalker...", LogType.Info);

                // Enable DLC
                gameFileCache.EnableDlc = true;
                
                // Set to the latest DLC if available
                if (gameFileCache.DlcNameList != null && gameFileCache.DlcNameList.Count > 0)
                {
                    var latestDlc = gameFileCache.DlcNameList[gameFileCache.DlcNameList.Count - 1];
                    gameFileCache.SelectedDlc = latestDlc;
                    LogHelper.Log($"Selected latest DLC: {latestDlc}", LogType.Info);
                }
                else
                {
                    LogHelper.Log("No DLC list available, enabling DLC with empty selection", LogType.Warning);
                    gameFileCache.SelectedDlc = string.Empty;
                }

                // Reinitialize active map RPF files to load DLC content
                var initActiveMapRpfFilesMethod = gameFileCache.GetType().GetMethod("InitActiveMapRpfFiles", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (initActiveMapRpfFilesMethod != null)
                {
                    initActiveMapRpfFilesMethod.Invoke(gameFileCache, null);
                    LogHelper.Log("Successfully reinitialized active RPF files with DLC content", LogType.Info);
                }
                else
                {
                    LogHelper.Log("Could not find InitActiveMapRpfFiles method, DLC content may not be fully loaded", LogType.Warning);
                }

                // Reinitialize peds to ensure DLC ped variations are loaded
                var initPedsMethod = gameFileCache.GetType().GetMethod("InitPeds", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (initPedsMethod != null)
                {
                    initPedsMethod.Invoke(gameFileCache, null);
                    LogHelper.Log("Successfully reinitialized peds with DLC content", LogType.Info);
                }
                else
                {
                    LogHelper.Log("Could not find InitPeds method", LogType.Warning);
                }

                LogHelper.Log($"DLC levels enabled successfully. DLC Active RPFs count: {gameFileCache.DlcActiveRpfs?.Count ?? 0}", LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Error enabling DLC levels: {ex.Message}", LogType.Error);
            }
        }

        private List<NativeDrawableInfo> GenerateNativeDrawables(List<string> includedCategories)
        {
            var nativeDrawables = new List<NativeDrawableInfo>();

            foreach (var category in includedCategories)
            {
                if (category == "props")
                {
                    // Handle props
                    foreach (Enums.SexType sex in Enum.GetValues<Enums.SexType>())
                    {
                        foreach (var propTypeName in EnumHelper.GetPropTypeList())
                        {
                            int propTypeNumeric = EnumHelper.GetValue(propTypeName, true);
                            int maxProps = SettingsHelper.Instance.GetDrawableTypeOffset(propTypeName, true, sex);
                            
                            if (maxProps > 0)
                            {
                                for (int propIndex = 0; propIndex < maxProps; propIndex++)
                                {
                                    // For native clothes, typically only texture 0 exists
                                    nativeDrawables.Add(new NativeDrawableInfo
                                    {
                                        TypeName = propTypeName,
                                        TypeNumeric = propTypeNumeric,
                                        Sex = sex,
                                        IsProp = true,
                                        DrawableIndex = propIndex,
                                        TextureIndex = 0
                                    });
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Handle components
                    foreach (Enums.SexType sex in Enum.GetValues<Enums.SexType>())
                    {
                        if (EnumHelper.GetDrawableTypeList().Contains(category))
                        {
                            int componentTypeNumeric = EnumHelper.GetValue(category, false);
                            int maxComponents = SettingsHelper.Instance.GetDrawableTypeOffset(category, false, sex);
                            
                            if (maxComponents > 0)
                            {
                                for (int componentIndex = 0; componentIndex < maxComponents; componentIndex++)
                                {
                                    // For native clothes, typically only texture 0 exists
                                    nativeDrawables.Add(new NativeDrawableInfo
                                    {
                                        TypeName = category,
                                        TypeNumeric = componentTypeNumeric,
                                        Sex = sex,
                                        IsProp = false,
                                        DrawableIndex = componentIndex,
                                        TextureIndex = 0
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return nativeDrawables;
        }

        private async Task<bool> WaitForScreenshotFile(string filename, int timeoutMs = 10000)
        {
            // Get the expected file path where screenshots are saved
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string screenshotDir = Path.Combine(documentsPath, "grzyClothTool", "Screenshots");
            string filePath = Path.Combine(screenshotDir, filename);

            LogHelper.Log($"Waiting for screenshot file: {filePath}", LogType.Info);

            // Wait for the file to exist with polling
            int elapsedMs = 0;
            const int pollIntervalMs = 100; // Increased poll interval
            int fileCheckAttempts = 0;

            while (elapsedMs < timeoutMs)
            {
                fileCheckAttempts++;
                
                if (File.Exists(filePath))
                {
                    try
                    {
                        // Check file size to ensure it's not empty
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > 0)
                        {
                            // Try to open the file to ensure it's fully written and not locked
                            using (var fileStream = File.OpenRead(filePath))
                            {
                                // File is accessible and has content
                                LogHelper.Log($"Screenshot file found and verified after {elapsedMs}ms: {filename} (Size: {fileInfo.Length} bytes)", LogType.Info);
                                return true;
                            }
                        }
                        else
                        {
                            LogHelper.Log($"Screenshot file exists but is empty (attempt {fileCheckAttempts}): {filename}", LogType.Warning);
                        }
                    }
                    catch (IOException ex)
                    {
                        LogHelper.Log($"Screenshot file exists but is locked (attempt {fileCheckAttempts}): {ex.Message}", LogType.Warning);
                        // File is still being written, continue waiting
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Log($"Error checking screenshot file (attempt {fileCheckAttempts}): {ex.Message}", LogType.Warning);
                    }
                }

                await Task.Delay(pollIntervalMs);
                elapsedMs += pollIntervalMs;
            }

            // Timeout reached - final check
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                LogHelper.Log($"Screenshot file exists at timeout but verification failed: {filename} (Size: {fileInfo.Length} bytes)", LogType.Warning);
                return fileInfo.Length > 0; // Return true if file exists and has content, even if we couldn't verify it fully
            }

            LogHelper.Log($"Screenshot file not found after {timeoutMs}ms timeout: {filename}", LogType.Error);
            return false;
        }

        private class NativeDrawableInfo
        {
            public string TypeName { get; set; }
            public int TypeNumeric { get; set; }
            public Enums.SexType Sex { get; set; }
            public bool IsProp { get; set; }
            public int DrawableIndex { get; set; }
            public int TextureIndex { get; set; }
        }

        #endregion
    }
}
