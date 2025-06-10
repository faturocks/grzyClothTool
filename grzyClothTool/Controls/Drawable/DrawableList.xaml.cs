using grzyClothTool.Extensions;
using grzyClothTool.Helpers;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static grzyClothTool.Controls.CustomMessageBox;
using ImageMagick;
using CodeWalker.GameFiles;
using grzyClothTool.Views;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Material.Icons.WPF;
using System.Threading;

namespace grzyClothTool.Controls
{
    /// <summary>
    /// Interaction logic for DrawableList.xaml
    /// </summary>
    public partial class DrawableList : UserControl, INotifyPropertyChanged
    {
        public event EventHandler DrawableListSelectedValueChanged;
        public event KeyEventHandler DrawableListKeyDown;
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached("ItemsSource", typeof(ObservableCollection<GDrawable>), typeof(DrawableList), new PropertyMetadata(default(ObservableCollection<GDrawable>)));

        public ObservableCollection<GDrawable> ItemsSource
        {
            get { return (ObservableCollection<GDrawable>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public object DrawableListSelectedValue => MyListBox.SelectedValue;

        public DrawableList()
        {
            InitializeComponent();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DrawableListSelectedValueChanged?.Invoke(sender, e);

            if (_ghostLineAdorner != null)
            {
                _adornerLayer?.Remove(_ghostLineAdorner);
                _ghostLineAdorner = null;
            }
        }

        private void DrawableList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            DrawableListKeyDown?.Invoke(sender, e);
        }

        private void OptimizeTexture_Click(object sender, RoutedEventArgs e)
        {
            //todo: Implement texture optimization logic here
        }

        private async void OptimizeDrawableTextures_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables.ToList();

            if (selectedDrawables.Count == 0)
            {
                CustomMessageBox.Show("No drawables selected.", "Optimize Textures", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            // Count total textures
            int totalTextures = selectedDrawables.Sum(d => d.Textures.Count);

            if (totalTextures == 0)
            {
                CustomMessageBox.Show("Selected drawables have no textures to optimize.", "Optimize Textures", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            var message = $"This will optimize {totalTextures} texture(s) from {selectedDrawables.Count} drawable(s).\n\n" +
                         "Each texture size will be divided by 2 (e.g., 1024x512 → 512x256).\n\n" +
                         "This operation cannot be undone. Do you want to continue?";

            var result = CustomMessageBox.Show(message, "Optimize Textures", CustomMessageBox.CustomMessageBoxButtons.YesNo);
            if (result != CustomMessageBox.CustomMessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ProgressHelper.Start();
                int processedTextures = 0;

                foreach (var drawable in selectedDrawables)
                {
                    foreach (var texture in drawable.Textures)
                    {
                        await OptimizeTextureSize(texture);
                        processedTextures++;
                        
                        // Update progress (simple percentage)
                        var progress = (processedTextures * 100) / totalTextures;
                        // Note: ProgressHelper might not support progress updates, but we can log it
                    }
                }

                ProgressHelper.Stop($"Optimized {processedTextures} texture(s) in {{0}}", true);
                SaveHelper.SetUnsavedChanges(true);

                // Refresh texture details for all optimized textures
                foreach (var drawable in selectedDrawables)
                {
                    foreach (var texture in drawable.Textures)
                    {
                        // Force reload texture details to reflect new size
                        texture.IsLoading = true;
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(100); // Small delay to ensure file is written
                            texture.IsLoading = false;
                        });
                    }
                }

                LogHelper.Log($"Successfully optimized {processedTextures} texture(s).", LogType.Info);
                CustomMessageBox.Show($"Successfully optimized {processedTextures} texture(s).\n\nNew sizes are half of the original dimensions.", "Optimization Complete", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
            }
            catch (Exception ex)
            {
                ProgressHelper.Stop("Texture optimization failed", false);
                LogHelper.Log($"Error optimizing textures: {ex.Message}", LogType.Error);
                CustomMessageBox.Show($"Error during texture optimization:\n{ex.Message}", "Optimization Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
            }
        }

        private async Task OptimizeTextureSize(GTexture texture)
        {
            try
            {
                // Get the current image
                using var img = ImgHelper.GetImage(texture.FilePath);
                if (img == null)
                {
                    LogHelper.Log($"Could not load texture: {texture.DisplayName}", LogType.Warning);
                    return;
                }

                // Calculate new dimensions (halved)
                int newWidth = Math.Max(1, img.Width / 2);
                int newHeight = Math.Max(1, img.Height / 2);

                // Ensure dimensions stay power of 2
                var (correctedWidth, correctedHeight) = ImgHelper.CheckPowerOfTwo(newWidth, newHeight);

                // Set up the image for DDS format
                img.Format = MagickFormat.Dds;
                img.Resize(correctedWidth, correctedHeight);

                // Set DDS properties
                var newMipMapCount = ImgHelper.GetCorrectMipMapAmount(correctedWidth, correctedHeight);
                img.Settings.SetDefine(MagickFormat.Dds, "mipmaps", newMipMapCount);
                img.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", true);

                // If it's a YTD file, we need to preserve the texture format
                if (texture.Extension == ".ytd")
                {
                    // Try to preserve the original compression format
                    var originalDetails = texture.TxtDetails;
                    if (originalDetails != null && !string.IsNullOrEmpty(originalDetails.Compression))
                    {
                        var compressionString = GetCompressionString(originalDetails.Compression);
                        img.Settings.SetDefine(MagickFormat.Dds, "compression", compressionString);
                    }
                    else
                    {
                        img.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                    }

                    // Create YTD file
                    var ytd = new YtdFile
                    {
                        TextureDict = new TextureDictionary()
                    };

                    var stream = new MemoryStream();
                    img.Write(stream);

                    var newDds = stream.ToArray();
                    var newTxt = CodeWalker.Utils.DDSIO.GetTexture(newDds);
                    newTxt.Name = texture.DisplayName;
                    ytd.TextureDict.BuildFromTextureList([newTxt]);

                    var bytes = ytd.Save();
                    await File.WriteAllBytesAsync(texture.FilePath, bytes);
                }
                else
                {
                    // For DDS, PNG, JPG files, save directly
                    img.Write(texture.FilePath);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Failed to optimize texture {texture.DisplayName}: {ex.Message}", LogType.Error);
                throw;
            }
        }

        private static string GetCompressionString(string cwCompression)
        {
            return cwCompression switch
            {
                "D3DFMT_DXT1" => "dxt1",
                "D3DFMT_DXT3" => "dxt3", 
                "D3DFMT_DXT5" => "dxt5",
                _ => "dxt5",
            };
        }

        private void MoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem?.Header is string addonName)
            {
                var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables.ToList();
                var addon = MainWindow.AddonManager.Addons.FirstOrDefault(a => a.Name == addonName);

                if (addon == null)
                {
                    return;
                }

                if (!addon.CanFitDrawables(selectedDrawables))
                {
                    Show("The selected addon cannot fit the selected drawables.", "Addon full", CustomMessageBoxButtons.OKOnly);
                    return;
                }

                foreach (var drawable in selectedDrawables)
                {
                    MainWindow.AddonManager.MoveDrawable(drawable, addon);
                }

                MainWindow.AddonManager.Addons.Sort(true);
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var drawable = DrawableListSelectedValue as GDrawable;
            FileHelper.OpenFileLocation(drawable?.FilePath);
        }

        private void DeleteDrawable_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables.ToList();
            MainWindow.AddonManager.DeleteDrawables(selectedDrawables);
        }

        private void ReplaceDrawable_Click(object sender, RoutedEventArgs e)
        {
            var drawable = DrawableListSelectedValue as GDrawable;

            OpenFileDialog files = new()
            {
                Title = $"Select drawable file to replace '{drawable.Name}'",
                Filter = "Drawable files (*.ydd)|*.ydd",
                Multiselect = false
            };

            if (files.ShowDialog() == true)
            {
                drawable.FilePath = files.FileName; // changing just path - might need to be updated to CreateDrawableAsync
                SaveHelper.SetUnsavedChanges(true);

                CWHelper.SendDrawableUpdateToPreview(e);
            }
        }

        private async void ExportDrawable_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables.ToList();

            MenuItem menuItem = sender as MenuItem;
            var tag = menuItem?.Tag?.ToString();

            OpenFolderDialog folder = new()
            {
                Title = tag switch
                {
                    "DDS" or "PNG" => $"Select the folder to export textures as {tag}",
                    "YTD" => "Select the folder to export drawable with textures",
                    "JSON" => "Select the folder to export debug info as JSON",
                    _ => "Select the folder to export drawable"
                },
                Multiselect = false
            };

            if (folder.ShowDialog() != true)
            {
                return;
            }

            string folderPath = folder.FolderName;

            try
            {
                if (tag == "JSON")
                {
                    await FileHelper.SaveDrawablesAsJsonAsync(selectedDrawables, folderPath).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrEmpty(tag) && (tag == "YTD" || tag == "PNG" || tag == "DDS"))
                {
                    foreach (var drawable in selectedDrawables)
                    {
                        await Task.Run(() => FileHelper.SaveTexturesAsync(new List<GTexture>(drawable.Textures), folderPath, tag).ConfigureAwait(false));
                    }

                    if (tag == "DDS" || tag == "PNG")
                    {
                        return;
                    }
                }

                await FileHelper.SaveDrawablesAsync(selectedDrawables, folderPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Drag and Drop

        private Point _dragStartPoint;
        private AdornerLayer _adornerLayer;
        private GhostLineAdorner _ghostLineAdorner;


        private void MyListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void MyListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListBox listBox = sender as ListBox;
                ListBoxItem listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (listBoxItem != null)
                {
                    if (_ghostLineAdorner != null)
                    {
                        _adornerLayer?.Remove(_ghostLineAdorner);
                        _ghostLineAdorner = null;
                    }

                    _ghostLineAdorner = new GhostLineAdorner(MyListBox);

                    _adornerLayer = AdornerLayer.GetAdornerLayer(MyListBox);
                    _adornerLayer?.Add(_ghostLineAdorner);

                    var selectedItem = listBox?.SelectedItem;

                    if (selectedItem is GDrawable)
                    {
                        DataObject data = new DataObject(typeof(GDrawable), selectedItem);
                        DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move);
                    }
                }
            }
        }

        private void MyListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            Point position = e.GetPosition(MyListBox);
            int index = GetCurrentIndex(position);

            if (_ghostLineAdorner != null)
            {
                if (index >= 0 && index < ItemsSource.Count)
                {
                    _ghostLineAdorner.UpdatePosition(index);
                }
            }

            var scrollViewer = FindChildOfType<ScrollViewer>(MyListBox);
            if (scrollViewer != null)
            {
                const double heightOfAutoScrollZone = 35;
                double mousePos = e.GetPosition(MyListBox).Y;

                if (mousePos < heightOfAutoScrollZone)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 1);
                }
                else if (mousePos > MyListBox.ActualHeight - heightOfAutoScrollZone)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 1);
                }
            }
        }

        private void MyListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(GDrawable)))
            {
                GDrawable droppedData = e.Data.GetData(typeof(GDrawable)) as GDrawable;
                ListBox listBox = sender as ListBox;
                GDrawable target = ((FrameworkElement)e.OriginalSource).DataContext as GDrawable;

                if (droppedData != null && target != null && ItemsSource != null)
                {
                    if (droppedData.Sex != target.Sex || droppedData.TypeNumeric != target.TypeNumeric || droppedData.IsProp != target.IsProp)
                    {
                        return;
                    }

                    int oldIndex = ItemsSource.IndexOf(droppedData);
                    int newIndex = ItemsSource.IndexOf(target);

                    if (oldIndex == newIndex)
                        return; // No movement needed

                    if (oldIndex < newIndex)
                    {
                        for (int i = oldIndex; i < newIndex; i++)
                        {
                            (ItemsSource[i + 1], ItemsSource[i]) = (ItemsSource[i], ItemsSource[i + 1]);
                        }
                    }
                    else
                    {
                        for (int i = oldIndex; i > newIndex; i--)
                        {
                            (ItemsSource[i - 1], ItemsSource[i]) = (ItemsSource[i], ItemsSource[i - 1]);
                        }
                    }

                    LogHelper.Log($"Drawable '{droppedData.Name}' moved from position {oldIndex} to {newIndex}");
                    MainWindow.AddonManager.SelectedAddon.Drawables.ReassignNumbers(droppedData);

                    MyListBox.SelectedItem = droppedData;
                    MyListBox.ScrollIntoView(droppedData);


                    _adornerLayer?.Remove(_ghostLineAdorner);
                    _ghostLineAdorner = null;
                }
            }
        }

        private int GetCurrentIndex(Point position)
        {
            int index = -1;
            for (int i = 0; i < MyListBox.Items.Count; i++)
            {
                ListBoxItem item = (ListBoxItem)MyListBox.ItemContainerGenerator.ContainerFromIndex(i);
                if (item != null)
                {
                    Rect bounds = VisualTreeHelper.GetDescendantBounds(item);
                    Point topLeft = item.TranslatePoint(new Point(), MyListBox);
                    Rect itemBounds = new(topLeft, bounds.Size);

                    if (itemBounds.Contains(position))
                    {
                        index = i;
                        break;
                    }
                }
            }
            return index;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                {
                    return t;
                }
                current = VisualTreeHelper.GetParent(current);
            };
            return null;
        }

        private static TChild FindChildOfType<TChild>(DependencyObject parent) where TChild : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                var result = (child as TChild) ?? FindChildOfType<TChild>(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is GDrawable drawable)
            {
                // Create basic tooltip immediately (without Details info)
                if (border.ToolTip == null)
                {
                    var tooltipPanel = new StackPanel();
                    
                    // Basic info (always available)
                    tooltipPanel.Children.Add(new TextBlock 
                    { 
                        Text = drawable.Name, 
                        FontWeight = FontWeights.Bold, 
                        Margin = new Thickness(0, 0, 0, 5) 
                    });
                    tooltipPanel.Children.Add(new TextBlock 
                    { 
                        Text = drawable.TypeName, 
                        Margin = new Thickness(0, 0, 0, 2) 
                    });
                    tooltipPanel.Children.Add(new TextBlock 
                    { 
                        Text = drawable.SexName, 
                        Margin = new Thickness(0, 0, 0, 2) 
                    });
                    
                    tooltipPanel.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });
                    
                    // Polygon count info - initially show loading message
                    tooltipPanel.Children.Add(new TextBlock 
                    { 
                        Text = "Polygon Count (Current / Limit):", 
                        FontWeight = FontWeights.Bold, 
                        Margin = new Thickness(0, 0, 0, 2) 
                    });
                    
                    var polygonInfoBlock = new TextBlock 
                    { 
                        FontFamily = new FontFamily("Consolas"),
                        Text = "Loading polygon information..."
                    };
                    tooltipPanel.Children.Add(polygonInfoBlock);
                    
                    border.ToolTip = tooltipPanel;
                    
                    // Subscribe to property changed event to clear tooltip when relevant properties change
                    PropertyChangedEventHandler propertyChangedHandler = null;
                    propertyChangedHandler = (s, args) =>
                    {
                        if (args.PropertyName == nameof(drawable.Name) || 
                            args.PropertyName == nameof(drawable.TypeName) || 
                            args.PropertyName == nameof(drawable.SexName) ||
                            args.PropertyName == nameof(drawable.Number))
                        {
                            // Clear the tooltip so it will be recreated with updated info on next hover
                            Dispatcher.BeginInvoke(() =>
                            {
                                border.ToolTip = null;
                                // Unsubscribe to prevent memory leaks
                                drawable.PropertyChanged -= propertyChangedHandler;
                            });
                        }
                    };
                    drawable.PropertyChanged += propertyChangedHandler;
                    
                    // Load details asynchronously and update tooltip when ready
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var details = await drawable.LoadDetailsOnDemandAsync();
                            
                            // Update UI on the UI thread
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (details != null && border.ToolTip == tooltipPanel) // Check if tooltip is still the same
                                {
                                    // Update polygon info
                                    polygonInfoBlock.Text = details.PolygonCountInfo ?? "No polygon information available";
                                    
                                    // Add warning info if available
                                    if (details.IsWarning && !string.IsNullOrEmpty(details.Tooltip))
                                    {
                                        tooltipPanel.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });
                                        tooltipPanel.Children.Add(new TextBlock { Text = details.Tooltip });
                                    }
                                    
                                    // Update warning icon
                                    if (border.FindName("WarningIcon") is MaterialIcon warningIcon)
                                    {
                                        warningIcon.Visibility = details.IsWarning ? Visibility.Visible : Visibility.Collapsed;
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading drawable details on hover: {ex.Message}");
                        }
                    });
                }
            }
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            // Optional: Clear tooltip to save memory
            // if (sender is Border border)
            // {
            //     border.ToolTip = null;
            // }
        }

        private async void TakeScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.AddonManager.IsPreviewEnabled)
            {
                CustomMessageBox.Show("Preview window is not open. Please open the 3D preview first.", "Take Screenshot", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            var selectedDrawable = DrawableListSelectedValue as GDrawable;
            if (selectedDrawable == null)
            {
                CustomMessageBox.Show("No drawable selected.", "Take Screenshot", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            if (selectedDrawable.Textures == null || selectedDrawable.Textures.Count == 0)
            {
                CustomMessageBox.Show("Selected drawable has no textures.", "Take Screenshot", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            try
            {
                string genderCode = selectedDrawable.Sex == Enums.SexType.male ? "M" : "F";
                string gameIdString = selectedDrawable.DisplayNumberWithOffset;

                for (int i = 0; i < selectedDrawable.Textures.Count; i++)
                {
                    var texture = selectedDrawable.Textures[i];
                    
                    // Update the preview to show current texture
                    MainWindow.AddonManager.SelectedAddon.SelectedTexture = texture;
                    CWHelper.SendDrawableUpdateToPreview(new DrawableUpdatedArgs
                    {
                        UpdatedName = "TextureChanged",
                        Value = texture.DisplayName
                    });

                    // Wait for preview to update
                    await Task.Delay(200);

                    // Generate filename with the specified format
                    string filename = $"{genderCode}_{selectedDrawable.TypeNumeric}_{gameIdString}_{i}.png";

                    // Take the screenshot with custom filename
                    bool success = CWHelper.TakeScreenshot(selectedDrawable.Name, filename);

                    if (!success)
                    {
                        CustomMessageBox.Show($"Failed to capture screenshot for texture {i + 1}: {texture.DisplayName}", 
                            "Screenshot Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                    }
                }

                CustomMessageBox.Show($"Successfully captured {selectedDrawable.Textures.Count} screenshot(s) for {selectedDrawable.Name}", 
                    "Screenshots Complete", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error during screenshot process: {ex.Message}", "Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
            }
        }

        private void FocusCamera_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.AddonManager.IsPreviewEnabled)
            {
                CustomMessageBox.Show("Preview window is not open. Please open the 3D preview first.", "Focus Camera", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            var selectedDrawable = DrawableListSelectedValue as GDrawable;
            if (selectedDrawable == null)
            {
                CustomMessageBox.Show("No drawable selected.", "Focus Camera", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                return;
            }

            try
            {
                bool success = CWHelper.FocusCameraOnDrawable();
                if (!success)
                {
                    CustomMessageBox.Show("Failed to focus camera. Make sure the preview window is active.", "Focus Camera", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error focusing camera: {ex.Message}", "Error", CustomMessageBox.CustomMessageBoxButtons.OKOnly);
            }
        }
    }

    public class GhostLineAdorner : Adorner
    {
        private readonly Rectangle _ghostLine;
        private int _index;

        public GhostLineAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _ghostLine = new Rectangle
            {
                Height = 4,
                Width = adornedElement.RenderSize.Width,
                Fill = Brushes.Black,
                Opacity = 1,
                StrokeThickness = 2,
                Stroke = Brushes.Black,
                IsHitTestVisible = false
            };
            AddVisualChild(_ghostLine);
        }

        public void UpdatePosition(int index)
        {
            _index = index;
            InvalidateArrange();
            InvalidateVisual();
            AdornedElement.InvalidateVisual();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => _ghostLine;

        protected override Size MeasureOverride(Size constraint)
        {
            _ghostLine.Measure(constraint);
            return new Size(constraint.Width, _ghostLine.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_index < 0) return finalSize;

            ListBox listBox = AdornedElement as ListBox;
            if (listBox == null) return finalSize;

            ListBoxItem item = listBox.ItemContainerGenerator.ContainerFromIndex(_index) as ListBoxItem;
            if (item != null)
            {
                Point relativePosition = item.TransformToAncestor(listBox).Transform(new Point(0, 0));
                double itemHeight = item.ActualHeight;

                double yOffset = relativePosition.Y + itemHeight;

                _ghostLine.Arrange(new Rect(new Point(0, yOffset), new Size(finalSize.Width, _ghostLine.Height)));
            }

            return finalSize;
        }
    }
}
