using grzyClothTool.Extensions;
using grzyClothTool.Helpers;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using grzyClothTool.Views;
using ImageMagick;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static grzyClothTool.Controls.CustomMessageBox;

namespace grzyClothTool.Controls
{
    public class DrawableUpdatedArgs : EventArgs
    {
        public string UpdatedName { get; set; }
        public dynamic Value { get; set; }
    }

    public class UpdatedEventArgs : EventArgs
    {
        public DependencyPropertyChangedEventArgs DependencyPropertyChangedEventArgs { get; set; }
        public bool IsUserInitiated { get; set; }
    }

    /// <summary>
    /// Interaction logic for SelectedDrawable.xaml
    /// </summary>
    public partial class SelectedDrawable : UserControl
    {
        public event EventHandler TextureListSelectedValueChanged;
        public event EventHandler<DrawableUpdatedArgs> SelectedDrawableUpdated;

        public static readonly DependencyProperty SelectedDrawableProperty =
        DependencyProperty.RegisterAttached("SelectedDraw", typeof(GDrawable), typeof(SelectedDrawable), new PropertyMetadata(default(GDrawable)));

        public static readonly DependencyProperty SelectedDrawablesProperty = 
            DependencyProperty.RegisterAttached("SelectedDrawables", typeof(ObservableCollection<GDrawable>), typeof(SelectedDrawable), new PropertyMetadata(default(ObservableCollection<GDrawable>)));

        public static readonly DependencyProperty SelectedTextureProperty =
        DependencyProperty.RegisterAttached("SelectedTxt", typeof(GTexture), typeof(SelectedDrawable), new PropertyMetadata(default(GTexture)));

        public static readonly DependencyProperty SelectedTexturesProperty =
         DependencyProperty.RegisterAttached("SelectedTextures", typeof(List<GTexture>), typeof(SelectedDrawable), new PropertyMetadata(default(List<GTexture>)));


        public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.RegisterAttached("SelectedIndex", typeof(int), typeof(SelectedDrawable), new PropertyMetadata(default(int)));

        public GDrawable SelectedDraw
        {
            get { return (GDrawable)GetValue(SelectedDrawableProperty);}
            set { SetValue(SelectedDrawableProperty, value); }
        }

        public ObservableCollection<GDrawable> SelectedDrawables
        {
            get { return (ObservableCollection<GDrawable>)GetValue(SelectedDrawablesProperty); }
            set { SetValue(SelectedDrawablesProperty, value); }
        }

        public GTexture SelectedTxt
        {
            get { return (GTexture)GetValue(SelectedTextureProperty); }
            set { SetValue(SelectedTextureProperty, value); }
        }

        public List<GTexture> SelectedTextures
        {
            get { return (List<GTexture>)GetValue(SelectedTexturesProperty); }
            set { SetValue(SelectedTexturesProperty, value); }
        }

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }

        public SelectedDrawable()
        {
            InitializeComponent();
        }

        private void TextureListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            SelectedTxt = listBox.SelectedItem as GTexture;
            SelectedTextures = listBox.SelectedItems.Cast<GTexture>().ToList();
            TextureListSelectedValueChanged?.Invoke(sender, e);

            if(SelectedTxt == null && listBox.Items.Count >= 1)
            {
                SelectedTxt = listBox.Items[0] as GTexture;
                SelectedTextures = [SelectedTxt];
            }
        }

        private void TextureListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Check for Ctrl+I key combination
            if (e.Key == Key.I && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ListBox listBox = sender as ListBox;
                if (listBox?.Items == null || listBox.Items.Count == 0)
                    return;

                // Get currently selected items
                var currentlySelected = new HashSet<object>(listBox.SelectedItems.Cast<object>());
                
                // Clear current selection
                listBox.SelectedItems.Clear();
                
                // Select all items that were NOT previously selected
                foreach (var item in listBox.Items)
                {
                    if (!currentlySelected.Contains(item))
                    {
                        listBox.SelectedItems.Add(item);
                    }
                }

                // Update our properties
                SelectedTxt = listBox.SelectedItem as GTexture;
                SelectedTextures = listBox.SelectedItems.Cast<GTexture>().ToList();
                
                // Mark the event as handled to prevent further processing
                e.Handled = true;
                
                LogHelper.Log("Texture selection inverted with Ctrl+I", LogType.Info);
            }
        }

        private void TexturePreview_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            GTexture gtxt = (GTexture)btn.DataContext;

            var textureListBox = FindTextureListBox(this);
            textureListBox.SelectedIndex = gtxt.TxtNumber;

            MagickImage img = ImgHelper.GetImage(gtxt.FilePath);
            if (img == null)
            {
                return;
            }

            int w = img.Width;
            int h = img.Height;
            byte[] pixels = img.ToByteArray(MagickFormat.Bgra);

            Bitmap bitmap = new(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            bitmap.UnlockBits(bitmapData);

            System.Windows.Controls.Image imageControl = new() { Stretch = Stretch.Uniform, Width = 400, Height = 300 };
            BitmapSource bitmapSource = BitmapSource.Create(
                bitmap.Width,
                bitmap.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgra32,
                null,
                pixels,
                bitmap.Width * 4
            );

            imageControl.Source = bitmapSource;

            TextBlock textBlock = new()
            {
                Text = $"{gtxt.DisplayName} ({w}x{h})",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            StackPanel stackPanel = new();
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(imageControl);

            Border border = new()
            {
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Black,

                Background = System.Windows.Media.Brushes.White,
                Child = stackPanel
            };

            Popup popup = new()
            {
                Width = 400,
                Height = 350,
                Placement = PlacementMode.Mouse,
                StaysOpen = false,
                Child = border,
                AllowsTransparency = true,

                IsOpen = true
            };
            popup.MouseMove += (s, args) =>
            {
                popup.IsOpen = false;
            };

            popup.Closed += (s, args) =>
            {
                bitmap.Dispose();
            };
        }

        // Used to notify CW ped viewer of changes to selected drawable
        private void SelectedDrawable_Updated(object sender, UpdatedEventArgs e)
        {
            if (!e.IsUserInitiated)
            {
                return;
            }

            var control = sender as Control;

            var args = new DrawableUpdatedArgs
            {
                UpdatedName = control.Tag.ToString(),
                Value = control.GetValue(e.DependencyPropertyChangedEventArgs.Property)
            };
            SelectedDrawableUpdated?.Invoke(control, args);
            SaveHelper.SetUnsavedChanges(true);


            // when multiple drawables selected, it doesn't update fields automatically, we have to set it from backend
            if (MainWindow.AddonManager.SelectedAddon.IsMultipleDrawablesSelected)
            {
                if (control is ModernLabelComboBox b && b.IsMultiSelect)
                {
                    // it's horrible but multiselect in ModernLabelComboBox needs to be handled differently
                    return;
                }

                var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables.ToList();
                foreach (var drawable in selectedDrawables)
                {
                    var property = drawable.GetType().GetProperty(args.UpdatedName);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(drawable, Convert.ChangeType(args.Value, property.PropertyType));
                    }
                }
            }
        }

        private static ListBox FindTextureListBox(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is ListBox listBox && listBox.Name == "TextureListBox")
                {
                    return listBox;
                }

                ListBox result = FindTextureListBox(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void DeleteTexture_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTextures != null)
            {
                var textureListBox = FindTextureListBox(this);
                int removedIndex = textureListBox.SelectedIndex;

                foreach (var texture in SelectedTextures)
                {
                    SelectedDraw.Textures.Remove(texture);

                    if (SettingsHelper.Instance.AutoDeleteFiles)
                    {
                        File.Delete(texture.FilePath);
                    }
                }
                SelectedDraw.Textures.ReassignNumbers();

                SaveHelper.SetUnsavedChanges(true);

                if (SelectedDraw.Textures.Count > 0)
                {
                    int newIndex = Math.Min(removedIndex, SelectedDraw.Textures.Count - 1);
                    textureListBox.SelectedIndex = newIndex;
                    SelectedTxt = textureListBox.SelectedItem as GTexture;
                    SelectedTextures = textureListBox.SelectedItems.Cast<GTexture>().ToList();
                }
                else
                {
                    SelectedTxt = null;
                    SelectedTextures = null;
                }
            }
        }

        private void AddTexture_Click(object sender, RoutedEventArgs e)
        {
            // calculate remaining texures that can be added
            int remainingTextures = GlobalConstants.MAX_DRAWABLE_TEXTURES - SelectedDraw.Textures.Count;
            if (remainingTextures <= 0)
            {
                Show($"You can't have more than {GlobalConstants.MAX_DRAWABLE_TEXTURES} textures per drawable!", "Error", CustomMessageBoxButtons.OKOnly, CustomMessageBoxIcon.Error);
                return;
            }

            OpenFileDialog files = new()
            {
                Title = $"Select textures",
                Filter = "Texture files (*.ytd)|*.ytd|Image files (*.jpg;*.png;*.dds)|*.jpg;*.png;*.dds",
                Multiselect = true
            };

            if (files.ShowDialog() == true)
            {
                foreach(var file in files.FileNames)
                {
                    // check if we are within the limit
                    if (remainingTextures <= 0)
                    {
                        // break the loop and show which texture was the last one
                        Show($"Reached the limit of {GlobalConstants.MAX_DRAWABLE_TEXTURES} textures. Last added texture: {Path.GetFileName(file)}.", "Info", CustomMessageBoxButtons.OKOnly, CustomMessageBoxIcon.Warning);
                        LogHelper.Log($"Reached the limit of {GlobalConstants.MAX_DRAWABLE_TEXTURES} textures. Last added texture: {Path.GetFileName(file)}.", LogType.Warning);
                        break;

                    }
                    var gtxt = new GTexture(file, SelectedDraw.TypeNumeric, SelectedDraw.Number, SelectedDraw.Textures.Count, SelectedDraw.HasSkin, SelectedDraw.IsProp);
                    SelectedDraw.Textures.Add(gtxt);

                    remainingTextures--;
                }

                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private void HandleTextureOptimization_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTextures == null)
                return;

            bool allOptimized = SelectedTextures.All(texture => texture.IsOptimizedDuringBuild);
            bool noneOptimized = SelectedTextures.All(texture => !texture.IsOptimizedDuringBuild);

            if (!allOptimized && !noneOptimized)
            {
                Show("Some textures are already optimized while others are not. Please select textures with the same state.", "Warning", CustomMessageBoxButtons.OKOnly, CustomMessageBoxIcon.Warning);
                return;
            }

            if (allOptimized)
            {
                UndoTextureOptimization();
            }
            else
            {
                OptimizeTextures();
            }

            SaveHelper.SetUnsavedChanges(true);
        }

        private void OptimizeTextures()
        {
            var wrongTextureName = OptimizeWindow.CheckTexturesHaveSameSize(SelectedTextures);
            if (wrongTextureName != null)
            {
                Show($"Texture {wrongTextureName} does not have the same size as the others!", "Error", CustomMessageBoxButtons.OKCancel, CustomMessageBoxIcon.Error);
                LogHelper.Log($"Texture {wrongTextureName} does not have the same size as the others!", LogType.Error);
                return;
            }

            var multipleSelected = SelectedTextures.Count > 1;
            var optimizeWindow = new OptimizeWindow(SelectedTextures, multipleSelected);
            optimizeWindow.ShowDialog();
        }

        private void UndoTextureOptimization()
        {
            foreach (var texture in SelectedTextures)
            {
                texture.IsOptimizedDuringBuild = false;

                // Deep clone the texture details
                texture.OptimizeDetails = new GTextureDetails
                {
                    Width = texture.TxtDetails.Width,
                    Height = texture.TxtDetails.Height,
                    MipMapCount = texture.TxtDetails.MipMapCount,
                    Compression = texture.TxtDetails.Compression,
                    Name = texture.TxtDetails.Name,
                    IsOptimizeNeeded = texture.TxtDetails.IsOptimizeNeeded,
                    IsOptimizeNeededTooltip = texture.TxtDetails.IsOptimizeNeededTooltip
                };

                LogHelper.Log($"Texture optimization for {texture.DisplayName} has been undone", LogType.Info);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedTxt != null)
            {
                FileHelper.OpenFileLocation(SelectedTxt.FilePath);
            }
        }

        private void DrawableType_Changed(object sender, UpdatedEventArgs e)
        {
            if (!e.IsUserInitiated)
            {
                return;
            }

            var newValue = e.DependencyPropertyChangedEventArgs.NewValue;
            var oldValue = e.DependencyPropertyChangedEventArgs.OldValue;

            if((newValue == null || oldValue == null) || newValue == oldValue)
            {
                return;
            }

            SelectedDraw.ChangeDrawableType(newValue.ToString());
        }

        private void DrawableSex_Changed(object sender, UpdatedEventArgs e)
        {
            if (!e.IsUserInitiated)
            {
                return;
            }

            var newValue = e.DependencyPropertyChangedEventArgs.NewValue;
            var oldValue = e.DependencyPropertyChangedEventArgs.OldValue;

            if((newValue == null || oldValue == null) || newValue == oldValue)
            {
                return;
            }

            SelectedDraw.ChangeDrawableSex(newValue.ToString());
        }

        private async void ReplaceReserved_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog file = new()
            {
                Title = $"Select drawable file to replace reserved",
                Filter = "Drawable file (*.ydd)|*.ydd"
            };

            if (file.ShowDialog() == true)
            {
                var newDrawable = await FileHelper.CreateDrawableAsync(file.FileName, SelectedDraw.Sex, SelectedDraw.IsProp, SelectedDraw.TypeNumeric, SelectedDraw.Number);

                // Replace reserved drawable with new drawable
                var index = MainWindow.AddonManager.SelectedAddon.Drawables.IndexOf(SelectedDraw);
                MainWindow.AddonManager.SelectedAddon.Drawables[index] = newDrawable;
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async void ExportTexture_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTextures == null)
            {
                return;
            }

            // Using tags to pass parameters. While functional, a cleaner approach (e.g., CommandParameter) may be preferred
            MenuItem menuItem = sender as MenuItem;
            var format = menuItem?.Tag?.ToString();


            // Make sure we got any format
            if (string.IsNullOrWhiteSpace(format))
            {
                return;
            }

            OpenFolderDialog folder = new()
            {
                Title = $"Select the folder to export textures as {format.ToUpper()}",
                Multiselect = false // Single folder selection
            };

            if (folder.ShowDialog() == true)
            {
                string folderPath = folder.FolderName;

                // Copy the textures to avoid accessing "SelectedTextures" from a background thread
                var texturesToExport = new List<GTexture>(SelectedTextures);

                try
                {
                    await Task.Run(() => FileHelper.SaveTexturesAsync(texturesToExport, folderPath, format));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReplaceTexture_Click(object sender, RoutedEventArgs e)
        {

            if (SelectedTextures == null || SelectedTextures.Count > 1) // TODO support multiple textures
            {
                return;
            }

            GTexture selectedTexture = SelectedTextures[0];

            OpenFileDialog file = new()
            {
                Title = $"Select texture file to replace {selectedTexture.DisplayName}",
                Filter = "Texture files (*.ytd)|*.ytd|Image files (*.jpg;*.png;*.dds)|*.jpg;*.png;*.dds" // we could store all available formats somewhere
            };

            if (file.ShowDialog() == false)
            {
                return;
            }

            // create new  texture
            var newTexture = new GTexture(file.FileName, SelectedDraw.TypeNumeric, SelectedDraw.Number, SelectedTextures[0].TxtNumber, SelectedDraw.HasSkin, SelectedDraw.IsProp);
            int index = SelectedDraw.Textures.IndexOf(selectedTexture);

            MainWindow.AddonManager.SelectedAddon.SelectedDrawable.Textures[index] = newTexture;
            MainWindow.AddonManager.SelectedAddon.SelectedTexture = newTexture;

            SelectedTxt = newTexture;
            SelectedTextures = new List<GTexture>([newTexture]);

            var textureListBox = FindTextureListBox(this);
            textureListBox.SelectedItem = newTexture;

            SaveHelper.SetUnsavedChanges(true);
            CWHelper.SendDrawableUpdateToPreview(e);
        }

        private void AllowOverride_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.AddonManager.SelectedAddon.AllowOverrideDrawables = true;
        }

        private void PositionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MoveDrawableToPosition(sender as TextBox);
                ((TextBox)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void PositionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            MoveDrawableToPosition(sender as TextBox);
        }

        private void MoveDrawableToPosition(TextBox textBox)
        {
            var drawable = MainWindow.AddonManager.SelectedAddon.SelectedDrawable;
            if (drawable == null) return;

            if (!int.TryParse(textBox.Text, out int newPosition))
            {
                // Reset to original value if invalid input
                textBox.Text = drawable.Number.ToString();
                return;
            }

            var drawables = MainWindow.AddonManager.SelectedAddon.Drawables;
            int oldIndex = drawables.IndexOf(drawable);
            int newIndex = -1;

            // Find the target index based on the new position
            for (int i = 0; i < drawables.Count; i++)
            {
                if (drawables[i].Number == newPosition)
                {
                    newIndex = i;
                    break;
                }
            }

            if (newIndex == -1 || newIndex == oldIndex)
            {
                // Reset to original value if position not found or no change
                textBox.Text = drawable.Number.ToString();
                return;
            }

            int oldPosition = drawable.Number;

            // Move the drawable using the same logic as drag and drop
            if (oldIndex < newIndex)
            {
                for (int i = oldIndex; i < newIndex; i++)
                {
                    (drawables[i + 1], drawables[i]) = (drawables[i], drawables[i + 1]);
                }
            }
            else
            {
                for (int i = oldIndex; i > newIndex; i--)
                {
                    (drawables[i - 1], drawables[i]) = (drawables[i], drawables[i - 1]);
                }
            }

            // Log the movement
            LogHelper.Log($"Drawable '{drawable.Name}' moved from position {oldPosition} to {newPosition}");
            
            // Reassign numbers using the same method as drag and drop
            drawables.ReassignNumbers(drawable);
            
            // Update the TextBox to show the correct position
            textBox.Text = drawable.Number.ToString();
            
            SaveHelper.SetUnsavedChanges(true);
        }

        private async Task<bool> WaitForScreenshotFile(string filename, int timeoutMs = 5000)
        {
            // Get the expected file path where screenshots are saved
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string screenshotDir = Path.Combine(documentsPath, "grzyClothTool", "Screenshots");
            string filePath = Path.Combine(screenshotDir, filename);

            // Wait for the file to exist with polling
            int elapsedMs = 0;
            const int pollIntervalMs = 50;

            while (elapsedMs < timeoutMs)
            {
                if (File.Exists(filePath))
                {
                    // File exists, now wait a bit more to ensure it's fully written
                    await Task.Delay(100);
                    
                    // Double-check by trying to open the file (ensures it's not locked)
                    try
                    {
                        using (var fileStream = File.OpenRead(filePath))
                        {
                            // If we can open it, the file is fully written
                            return true;
                        }
                    }
                    catch (IOException)
                    {
                        // File is still being written, wait a bit more
                        await Task.Delay(pollIntervalMs);
                        elapsedMs += pollIntervalMs;
                        continue;
                    }
                }

                await Task.Delay(pollIntervalMs);
                elapsedMs += pollIntervalMs;
            }

            // Timeout reached
            return false;
        }

        private async void TakeAllScreenshots_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.AddonManager.IsPreviewEnabled)
            {
                CustomMessageBox.Show("Preview window is not open. Please open the 3D preview first.", "Take All Screenshots", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Show category selection dialog (same as DELETE HIGH POLYGON MODELS)
            var includedCategories = ShowCategorySelectionDialog();
            if (includedCategories == null) // User cancelled
                return;

            // Collect all drawables from selected categories
            var drawablesToScreenshot = new List<GDrawable>();
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

                    // Skip if no textures
                    if (drawable.Textures == null || drawable.Textures.Count == 0)
                        continue;

                    drawablesToScreenshot.Add(drawable);
                }
            }

            if (drawablesToScreenshot.Count == 0)
            {
                CustomMessageBox.Show("No eligible clothes found in the selected categories.", "Take All Screenshots", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Calculate total screenshots that will be taken
            int totalScreenshots = drawablesToScreenshot.Sum(d => d.Textures.Count);

            var message = $"This will take {totalScreenshots} screenshot(s) from {drawablesToScreenshot.Count} drawable(s).\n\n" +
                         "Included categories: " + string.Join(", ", includedCategories) + "\n\n" +
                         "Each texture variation will be captured automatically.\n\n" +
                         "Do you want to continue?";

            var result = CustomMessageBox.Show(message, "Take All Screenshots", CustomMessageBox.CustomMessageBoxButtons.YesNo);
            if (result != CustomMessageBox.CustomMessageBoxResult.Yes)
                return;

            try
            {
                // Optimize CodeWalker UI for batch screenshots
                CWHelper.OptimizeCodeWalkerForScreenshots();
                
                int successCount = 0;
                int totalCount = 0;

                foreach (var drawable in drawablesToScreenshot)
                {
                    // Properly simulate UI selection by updating the SelectedDrawables collection
                    // This replicates what happens when user clicks on a drawable in the list
                    MainWindow.AddonManager.SelectedAddon.SelectedDrawables.Clear();
                    MainWindow.AddonManager.SelectedAddon.SelectedDrawables.Add(drawable);
                    MainWindow.AddonManager.SelectedAddon.SelectedDrawable = drawable;
                    
                    // Set the first texture as selected (same as what happens in UI selection)
                    if (drawable.Textures.Count > 0)
                    {
                        MainWindow.AddonManager.SelectedAddon.SelectedTexture = drawable.Textures.First();
                    }
                    
                    // Create proper selection changed event args to simulate UI click
                    var drawableSelectionArgs = new SelectionChangedEventArgs(
                        System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                        new GDrawable[] { }, // removed items
                        new GDrawable[] { drawable } // added items
                    );
                    
                    // Send drawable update to preview (same as when user clicks on drawable)
                    CWHelper.SendDrawableUpdateToPreview(drawableSelectionArgs);
                    
                    // Brief wait for drawable to load (GDI is much faster)
                    await Task.Delay(50);

                    string genderCode = drawable.Sex == Enums.SexType.male ? "M" : "F";
                    string gameIdString = drawable.DisplayNumberWithOffset;

                    for (int i = 0; i < drawable.Textures.Count; i++)
                    {
                        var texture = drawable.Textures[i];
                        totalCount++;

                        // Simulate user selecting the texture in UI
                        MainWindow.AddonManager.SelectedAddon.SelectedTexture = texture;

                        // Create proper selection changed event args for texture
                        var textureSelectionArgs = new SelectionChangedEventArgs(
                            System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                            new GTexture[] { }, // removed items  
                            new GTexture[] { texture } // added items
                        );

                        // Send texture update to preview (same mechanism as SelectedDrawable_TextureChanged)
                        if (MainWindow.AddonManager.IsPreviewEnabled)
                        {
                            // Check if the drawable exists in LoadedDrawables before accessing it
                            if (!CWHelper.CWForm.LoadedDrawables.ContainsKey(drawable.Name))
                            {
                                // If drawable is not loaded, load it first
                                var ydd = CWHelper.CreateYddFile(drawable);
                                if (ydd != null && ydd.Drawables.Length > 0)
                                {
                                    CWHelper.CWForm.LoadedDrawables[drawable.Name] = ydd.Drawables.First();
                                }
                                else
                                {
                                    continue; // Skip this texture if drawable can't be loaded
                                }
                            }

                            var ytd = CWHelper.CreateYtdFile(texture, texture.DisplayName);
                            var cwydd = CWHelper.CWForm.LoadedDrawables[drawable.Name];
                            CWHelper.CWForm.LoadedTextures[cwydd] = ytd.TextureDict;
                            CWHelper.CWForm.Refresh();
                        }
                        
                        // Brief wait for texture to load
                        await Task.Delay(25);

                        // Generate filename with the specified format
                        string filename = $"{genderCode}_{drawable.TypeNumeric}_{gameIdString}_{i}.png";

                        // Take screenshot using GDI method
                        bool success = CWHelper.TakeScreenshot(drawable.Name, filename);
                        LogHelper.Log($"Screenshot sonucu: {success} for {filename}", LogType.Info);

                        if (success)
                        {
                            // Wait for the screenshot file to be actually saved to disk
                            bool fileSaved = await WaitForScreenshotFile(filename);
                            if (fileSaved)
                            {
                                successCount++;
                            }
                            else
                            {
                                LogHelper.Log($"Screenshot file {filename} was not saved within timeout period", LogType.Warning);
                            }
                        }

                        // Minimal delay between screenshots (GDI is fast)
                        await Task.Delay(10);
                    }

                    // Brief delay between drawables
                    await Task.Delay(25);

                    // Clean up memory for the previous drawable before moving to next one
                    CleanupDrawableMemory(drawable);
                }

                CustomMessageBox.Show($"Successfully captured {successCount} out of {totalCount} screenshot(s).", 
                    "Screenshots Complete", CustomMessageBox.CustomMessageBoxButtons.OKOnly);

                LogHelper.Log($"Automated screenshot process completed: {successCount}/{totalCount} successful", LogType.Info);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error during automated screenshot process: {ex.Message}", "Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                LogHelper.Log($"Automated screenshot error: {ex.Message}", LogType.Error);
            }
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



        private bool TakeCoreScreenshot(string drawableName, string customFilename)
        {
            try
            {
                if (CWHelper.CWForm == null || CWHelper.CWForm.IsDisposed || !CWHelper.CWForm.formopen)
                {
                    return false;
                }

                // Create screenshot directory if it doesn't exist
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string screenshotDir = Path.Combine(documentsPath, "grzyClothTool", "Screenshots");
                Directory.CreateDirectory(screenshotDir);

                // Generate full file path
                string fileName = customFilename;
                if (!fileName.EndsWith(".png"))
                {
                    fileName += ".png";
                }
                string filePath = Path.Combine(screenshotDir, fileName);

                // Brief delay for UI stability with GDI capture
                System.Threading.Thread.Sleep(25);

                // Call ONLY the absolute core capture method - ProcessTransparentScreenshotData
                var cwFormType = CWHelper.CWForm.GetType();
                
                // Use the simple GDI screenshot method
                return CWHelper.TakeScreenshot(drawableName, customFilename);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Core screenshot failed: {ex.Message}", LogType.Error);
                return false;
            }
        }

                private bool CaptureBackbufferDirectly(string filePath, object renderer)
        {
            try
            {
                // SIMPLE APPROACH: Just call regular screenshot method since we already setup CodeWalker
                // The setup was done once, so this should work without restoration messages
                return CWHelper.TakeScreenshot("temp", System.IO.Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Simple screenshot capture failed: {ex.Message}", LogType.Error);
                return false;
            }
        }

        private bool SaveBackbufferAsPng(object backBuffer, string filePath, object context)
        {
            try
            {
                // This is getting very complex for direct D3D11 texture saving
                // Let's use a simpler approach - just save what's currently rendered
                
                // For now, let's just return false and use a different approach
                LogHelper.Log("Direct backbuffer saving not implemented", LogType.Warning);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Backbuffer save failed: {ex.Message}", LogType.Error);
                return false;
            }
        }





        private void CleanupDrawableMemory(GDrawable drawable)
        {
            try
            {
                if (CWHelper.CWForm == null || CWHelper.CWForm.IsDisposed || !CWHelper.CWForm.formopen)
                {
                    return;
                }

                // Clean up loaded drawable from CodeWalker memory
                if (CWHelper.CWForm.LoadedDrawables.ContainsKey(drawable.Name))
                {
                    var loadedDrawable = CWHelper.CWForm.LoadedDrawables[drawable.Name];
                    
                    // Clean up loaded textures for this drawable
                    if (CWHelper.CWForm.LoadedTextures.ContainsKey(loadedDrawable))
                    {
                        // Remove from loaded textures dictionary
                        CWHelper.CWForm.LoadedTextures.Remove(loadedDrawable);
                    }
                    
                    // Remove from loaded drawables dictionary
                    CWHelper.CWForm.LoadedDrawables.Remove(drawable.Name);
                }

                // Force garbage collection to free up GPU memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Try to clear CodeWalker's renderer cache for better memory management
                try
                {
                    var rendererField = CWHelper.CWForm.GetType().GetField("Renderer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (rendererField?.GetValue(CWHelper.CWForm) != null)
                    {
                        var renderer = rendererField.GetValue(CWHelper.CWForm);
                        var cacheField = renderer.GetType().GetField("RenderableCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (cacheField?.GetValue(renderer) != null)
                        {
                            var cache = cacheField.GetValue(renderer);
                            var clearMethod = cache.GetType().GetMethod("ClearCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            clearMethod?.Invoke(cache, null);
                        }
                    }
                }
                catch 
                { 
                    // If we can't clear the cache, it's not critical
                }

                LogHelper.Log($"Cleaned up memory for drawable: {drawable.Name}", LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Error cleaning up memory for drawable {drawable.Name}: {ex.Message}", LogType.Warning);
            }
        }
    }
}
