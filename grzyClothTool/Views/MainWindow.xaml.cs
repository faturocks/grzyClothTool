using grzyClothTool.Helpers;
using grzyClothTool.Models;
using grzyClothTool.Views;
using Material.Icons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using static grzyClothTool.Enums;
using System.Text.Json;

namespace grzyClothTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static MainWindow _instance;
        public static MainWindow Instance => _instance;
        private static NavigationHelper _navigationHelper;
        public static NavigationHelper NavigationHelper => _navigationHelper;

        private static AddonManager _addonManager;
        public static AddonManager AddonManager => _addonManager;

        public MainWindow()
        {
            InitializeComponent();
            this.Visibility = Visibility.Hidden;
            CWHelper.Init();
            _ = TelemetryHelper.LogSession(true);

            _instance = this;
            _addonManager = new AddonManager();

            _navigationHelper = new NavigationHelper();
            _navigationHelper.RegisterPage("Home", () => new Home());
            _navigationHelper.RegisterPage("Project", () => new ProjectWindow());
            _navigationHelper.RegisterPage("Settings", () => new SettingsWindow());

            DataContext = _navigationHelper;
            _navigationHelper.Navigate("Home");
            version.Header = "Version: " + UpdateHelper.GetCurrentVersion();

            FileHelper.GenerateReservedAssets();
            LogHelper.Init();
            LogHelper.LogMessageCreated += LogHelper_LogMessageCreated;
            ProgressHelper.ProgressStatusChanged += ProgressHelper_ProgressStatusChanged;

            SaveHelper.Init();

            Dispatcher.BeginInvoke((Action)(async () =>
            {
#if !DEBUG
                App.splashScreen.AddMessage("Checking for updates...");
                await UpdateHelper.CheckForUpdates();
#endif
                App.splashScreen.AddMessage("Starting app");

                // Wait until the SplashScreen's message queue is empty
                while (App.splashScreen.MessageQueueCount > 0)
                {
                    await Task.Delay(2000);
                }

                await App.splashScreen.LoadComplete();
            }));
        }

        private void ProgressHelper_ProgressStatusChanged(object sender, ProgressMessageEventArgs e)
        {
            var visibility = e.Status switch
            {
                ProgressStatus.Start => Visibility.Visible,
                ProgressStatus.Stop => Visibility.Hidden,
                _ => Visibility.Collapsed
            };

            this.Dispatcher.Invoke(() =>
            {
                progressBar.Visibility = visibility;
            });
        }

        private void LogHelper_LogMessageCreated(object sender, LogMessageEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                logBar.Text = e.Message;
                logBarIcon.Kind = Enum.Parse<MaterialIconKind>(e.TypeIcon);
                logBarIcon.Visibility = Visibility.Visible;
            });
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.FileName = e.Uri.AbsoluteUri;
            p.Start();
        }

        //this is needed so window can be clicked anywhere to unfocus textbox
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            FocusManager.SetFocusedElement(this, this);
        }

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).Tag.ToString();

            _navigationHelper.Navigate(tag);
        }

        public void OpenAddon_Click(object sender, RoutedEventArgs e)
        {
            _ = OpenAddonAsync();
        }

        public async Task OpenAddonAsync(bool shouldSetProjectName = false)
        {
            if (!SaveHelper.CheckUnsavedChangesMessage())
            {
                return;
            }

            OpenFileDialog metaFiles = new()
            {
                Title = "Select .meta file(s)",
                Multiselect = true,
                Filter = "Meta files (*.meta)|*.meta"
            };

            if (metaFiles.ShowDialog() == true)
            {
                ProgressHelper.Start("Started loading addon");

                // Opening existing addon, should clear everything and add new opened ones
                AddonManager.Addons = [];
                foreach (var dir in metaFiles.FileNames)
                {
                    using (var reader = new StreamReader(dir))
                    {
                        string firstLine = await reader.ReadLineAsync();
                        string secondLine = await reader.ReadLineAsync();

                        //Check two first lines if it contains "ShopPedApparel"
                        if ((firstLine == null || !firstLine.Contains("ShopPedApparel")) &&
                            (secondLine == null || !secondLine.Contains("ShopPedApparel")))
                        {
                            LogHelper.Log($"Skipped file {dir} as it is probably not a correct .meta file");
                            return;
                        }
                    }

                    await AddonManager.LoadAddon(dir, shouldSetProjectName);
                }

                ProgressHelper.Stop("Addon loaded in {0}", true);
                SaveHelper.SetUnsavedChanges(true);
            }
        }

        private async void OpenSave_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is SaveFile saveFile)
            {
                if(!SaveHelper.CheckUnsavedChangesMessage())
                {
                    return;
                }

                await SaveHelper.LoadAsync(saveFile);
                NavigationHelper.Navigate("Project");
            }
        }

        private async void LoadProject_Click(object sender, RoutedEventArgs e)
        {
            bool success = await LoadProjectAsync();
            if (success)
            {
                // Auto-navigate to Project window if loaded from main menu
                NavigationHelper.Navigate("Project");
            }
        }

        public async Task<bool> LoadProjectAsync(bool shouldSetProjectName = false)
        {
            if (!SaveHelper.CheckUnsavedChangesMessage())
            {
                return false;
            }

            OpenFileDialog openFileDialog = new()
            {
                Title = "Load project",
                Filter = "grzyClothTool project (*.gctproject)|*.gctproject"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Disable auto-saving during project load to avoid conflicts
                SaveHelper.SavingPaused = true;
                ProgressHelper.Start($"Loading {openFileDialog.SafeFileName}");

                try
                {
                    var json = await File.ReadAllTextAsync(openFileDialog.FileName);
                    var addonManager = JsonSerializer.Deserialize<AddonManager>(json, SaveHelper.SerializerOptions);

                    // Set project name from loaded data or filename
                    if (shouldSetProjectName)
                    {
                        if (!string.IsNullOrWhiteSpace(addonManager.ProjectName))
                        {
                            AddonManager.ProjectName = addonManager.ProjectName;
                        }
                        else
                        {
                            // Use filename as project name if not set in the project data
                            AddonManager.ProjectName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                        }
                    }

                    // Clear existing addons and load from project
                    AddonManager.Addons.Clear();
                    foreach (var addon in addonManager.Addons)
                    {
                        AddonManager.Addons.Add(addon);
                    }

                    // Show warning message about loaded project
                    var projectName = !string.IsNullOrWhiteSpace(AddonManager.ProjectName) ? AddonManager.ProjectName : "Unnamed Project";
                    LogHelper.Log($"Project '{projectName}' loaded successfully! Auto-save has been resumed.", Views.LogType.Warning);
                    
                    ProgressHelper.Stop("Project loaded in {0}", true);
                    SaveHelper.SetUnsavedChanges(true);
                    
                    return true; // Success
                }
                catch (Exception ex)
                {
                    LogHelper.Log($"Error loading project: {ex.Message}", Views.LogType.Warning);
                    ProgressHelper.Stop("Failed to load project", false);
                    return false; // Failure
                }
                finally
                {
                    // Re-enable auto-saving
                    SaveHelper.SavingPaused = false;
                }
            }

            return false; // User cancelled
        }

        private async void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            var savedProjectName = string.IsNullOrWhiteSpace(AddonManager.ProjectName) ? "project" : AddonManager.ProjectName;
            SaveFileDialog saveFileDialog = new()
            {
                Title = "Save project",
                Filter = "grzyClothTool project (*.gctproject)|*.gctproject",
                FileName = $"{savedProjectName}.gctproject"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ProgressHelper.Start("Saving project");

                try
                {
                    var json = JsonSerializer.Serialize(AddonManager, SaveHelper.SerializerOptions);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);

                    ProgressHelper.Stop("Project saved in {0}", true);
                }
                catch (Exception ex)
                {
                    LogHelper.Log($"Error saving project: {ex.Message}", Views.LogType.Warning);
                    ProgressHelper.Stop("Failed to save project", false);
                }
            }
        }

        // if main window is closed, close CW window too
        private void Window_Closed(object sender, System.EventArgs e)
        {
            _ = TelemetryHelper.LogSession(false);

            if (CWHelper.CWForm.formopen)
            {
                CWHelper.CWForm.Close();
            }

            LogHelper.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!SaveHelper.CheckUnsavedChangesMessage())
            {
                e.Cancel = true;
            }
        }

        private void StatusBarItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LogHelper.OpenLogWindow();
        }

        private void LogsOpen_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.OpenLogWindow();
        }
    }
}
