using CodeWalker;
using CodeWalker.GameFiles;
using grzyClothTool.Controls;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using grzyClothTool.Extensions;
using grzyClothTool.Helpers;
using grzyClothTool.Views;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;

namespace grzyClothTool.Helpers;
public static class CWHelper
{
    public static CustomPedsForm CWForm;
    public static string GTAVPath => GTAFolder.GetCurrentGTAFolderWithTrailingSlash();
    public static bool IsCacheStartupEnabled => Properties.Settings.Default.GtaCacheStartup;

    private static readonly YtdFile _ytdFile = new();

    private static Enums.SexType PrevDrawableSex;

    // Win32 API declarations for screenshot capture
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private const uint SRCCOPY = 0x00CC0020;

    public static void Init()
    {
        var isFolderValid = GTAFolder.IsCurrentGTAFolderValid();
        if(!isFolderValid)
        {
            var folder = GTAFolder.AutoDetectFolder();
            if (folder != null)
            {
                SetGTAFolder(folder);
            }
        }

        if (IsCacheStartupEnabled)
        {
            // todo: load cw cache
        }

        CWForm = new CustomPedsForm();
    }

    public static void SetGTAFolder(string path)
    {
        GTAFolder.SetGTAFolder(path);
    }

    public static void SetCacheStartup(bool value)
    {
        Properties.Settings.Default.GtaCacheStartup = value;
        Properties.Settings.Default.Save();
    }

    public static YtdFile GetYtdFile(string path)
    {
        _ytdFile.Load(File.ReadAllBytes(path));
        return _ytdFile;
    }

    public static YtdFile CreateYtdFile(GTexture texture, string name)
    {
        byte[] data = texture.Extension switch
        {
            ".ytd" => File.ReadAllBytes(texture.FilePath), // Read existing YTD file directly
            ".png" or ".jpg" or ".dds" => ImgHelper.GetDDSBytes(texture), // Create DDS texture
            _ => throw new NotSupportedException($"Unsupported file extension: {texture.Extension}"),
        };

        var ytdFile = new YtdFile();
        ytdFile.Load(data);
        return ytdFile;
    }

    public static YddFile CreateYddFile(GDrawable drawable)
    {
        byte[] data = File.ReadAllBytes(drawable.FilePath);

        var yddFile = new YddFile();
        yddFile.Load(data);
        return yddFile;
    }

    public static void SetPedModel(Enums.SexType sex)
    {
        if (CWForm == null) return;

        var pedModel = sex == Enums.SexType.male ? "mp_m_freemode_01" : "mp_f_freemode_01";
        CWForm.PedModel = pedModel;

        PrevDrawableSex = sex;
    }

    public static void SendDrawableUpdateToPreview(EventArgs args)
    {
        if (!CWForm.formopen || CWForm.isLoading) return;

        var selectedDrawables = MainWindow.AddonManager.SelectedAddon.SelectedDrawables;

        // Don't send anything if no drawables are selected
        if (selectedDrawables.Count == 0) return;

        Dictionary<string, string> updateDict = [];
        if (args is DrawableUpdatedArgs dargs)
        {
            updateDict[dargs.UpdatedName] = dargs.Value.ToString();
        }

        // Identify drawables that are no longer selected and remove them
        var selectedNames = selectedDrawables.Select(d => d.Name).ToHashSet();
        var removedDrawables = CWForm.LoadedDrawables.Keys.Where(name => !selectedNames.Contains(name)).ToList();
        foreach (var removed in removedDrawables)
        {

            if (CWForm.LoadedDrawables.TryGetValue(removed, out var removedDrawable))
            {
                CWForm.LoadedTextures.Remove(removedDrawable);
            }
            CWForm.LoadedDrawables.Remove(removed);
        }

        if (selectedDrawables.Count == 1)
        {
            var firstSelected = selectedDrawables.First();
            if (PrevDrawableSex != firstSelected.Sex)
            {
                SetPedModel(firstSelected.Sex);
                updateDict.Add("GenderChanged", "");
            }
        }

        // Add or update selected drawables and their textures
        foreach (var drawable in selectedDrawables)
        {
            var ydd = CreateYddFile(drawable);
            if (ydd == null || ydd.Drawables.Length == 0) continue;

            var firstDrawable = ydd.Drawables.First();
            CWForm.LoadedDrawables[drawable.Name] = firstDrawable;

            GTexture selectedTexture = MainWindow.AddonManager.SelectedAddon.SelectedTexture;
            YtdFile ytd = null;
            if (selectedTexture != null)
            {
                ytd = CreateYtdFile(selectedTexture, selectedTexture.DisplayName);
                CWForm.LoadedTextures[firstDrawable] = ytd.TextureDict;
            }

            if (selectedTexture == null && selectedDrawables.Count > 1)
            {
                // If multiple drawables are selected, we need to load the first texture of the first drawable
                // to prevent the preview from being empty
                var firstTexture = drawable.Textures.FirstOrDefault();
                if (firstTexture != null)
                {
                    ytd = CreateYtdFile(firstTexture, firstTexture.DisplayName);
                    CWForm.LoadedTextures[firstDrawable] = ytd.TextureDict;
                }
            }

            CWForm.UpdateSelectedDrawable(
                firstDrawable,
                ytd.TextureDict,
                updateDict
            );
        }
    }

    /// <summary>
    /// Automatically optimizes CodeWalker UI settings for screenshots
    /// - Closes tools panel for cleaner screenshots
    /// - Enables "Selected Drawable Only" for better focus
    /// </summary>
    public static void OptimizeCodeWalkerForScreenshots()
    {
        try
        {
            if (CWForm == null || CWForm.IsDisposed || !CWForm.formopen)
            {
                return;
            }

            LogHelper.Log("Optimizing CodeWalker UI for screenshots...", LogType.Info);

            var cwFormType = CWForm.GetType();

            // Close tools panel for cleaner screenshots
            var toolsPanelField = cwFormType.GetField("ToolsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (toolsPanelField?.GetValue(CWForm) is System.Windows.Forms.Control toolsPanel)
            {
                if (toolsPanel.Visible)
                {
                    toolsPanel.Visible = false;
                    LogHelper.Log("Tools panel closed for cleaner screenshots", LogType.Info);
                }
            }

            // Enable "Selected Drawable Only" for better focus
            var onlySelectedField = cwFormType.GetField("OnlySelectedCheckBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (onlySelectedField?.GetValue(CWForm) is System.Windows.Forms.CheckBox onlySelectedCheckBox)
            {
                if (!onlySelectedCheckBox.Checked)
                {
                    onlySelectedCheckBox.Checked = true;
                    LogHelper.Log("'Selected Drawable Only' enabled for focused screenshots", LogType.Info);
                }
            }

            // Hide console panel if visible for cleaner screenshots
            var consolePanelField = cwFormType.GetField("ConsolePanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (consolePanelField?.GetValue(CWForm) is System.Windows.Forms.Control consolePanel)
            {
                if (consolePanel.Visible)
                {
                    consolePanel.Visible = false;
                    LogHelper.Log("Console panel hidden for cleaner screenshots", LogType.Info);
                }
            }

            LogHelper.Log("CodeWalker UI optimization completed", LogType.Info);
        }
        catch (Exception ex)
        {
            LogHelper.Log($"Failed to optimize CodeWalker UI: {ex.Message}", LogType.Warning);
        }
    }

    /// <summary>
    /// Takes a screenshot using the enhanced alpha mask method
    /// For each cloth, generates alpha mask once in Vertex Colour 2 mode, then applies it to all texture variations
    /// </summary>
    /// <param name="drawableName">Name of the drawable being captured</param>
    /// <param name="fileName">Name for the screenshot file</param>
    /// <param name="useAlphaMask">Whether to use alpha mask approach (default: true)</param>
    /// <returns>True if screenshot was taken successfully</returns>
    public static bool TakeScreenshot(string drawableName, string fileName, bool useAlphaMask = true)
    {
        try
        {
            LogHelper.Log($"Starting TakeScreenshot for {drawableName} (AlphaMask: {useAlphaMask})", LogType.Info);
            
            if (CWForm == null)
            {
                LogHelper.Log("ERROR: CWForm is null", LogType.Error);
                return false;
            }
            
            // Automatically optimize CodeWalker UI for screenshots
            OptimizeCodeWalkerForScreenshots();
            
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultScreenshotDir = Path.Combine(documentsPath, "grzyClothTool", "Screenshots");
            
            // Ensure directory exists
            if (!Directory.Exists(defaultScreenshotDir))
            {
                Directory.CreateDirectory(defaultScreenshotDir);
            }
            
            string fullFilePath = Path.Combine(defaultScreenshotDir, fileName);
            LogHelper.Log($"Full file path: {fullFilePath}", LogType.Info);
            
            // Brief delay for UI refresh with GDI capture  
            System.Threading.Thread.Sleep(50);
            
            // Use the enhanced alpha mask screenshot method
            bool success;
            if (useAlphaMask)
            {
                LogHelper.Log("Using alpha mask screenshot method", LogType.Info);
                success = CWForm.TakeGDIScreenshotWithAlphaMask(fullFilePath, drawableName, true);
            }
            else
            {
                LogHelper.Log("Using direct blue removal method", LogType.Info);
                success = CWForm.TakeGDIScreenshot(fullFilePath);
            }
            
            LogHelper.Log($"Screenshot method completed, result: {success}", LogType.Info);
            
            if (!success)
            {
                LogHelper.Log($"WARNING: Screenshot method returned false for: {fileName}", LogType.Warning);
                
                // Check if file was actually created despite returning false
                if (File.Exists(fullFilePath))
                {
                    LogHelper.Log($"INFO: File was created despite method returning false: {fullFilePath}", LogType.Info);
                    return true;
                }
            }
            
            System.Threading.Thread.Sleep(25); // Brief delay for stability
            
            return success;
        }
        catch (Exception ex)
        {
            LogHelper.Log($"ERROR: TakeScreenshot failed: {ex.Message}", LogType.Error);
            LogHelper.Log($"ERROR: Exception stack trace: {ex.StackTrace}", LogType.Error);
            return false;
        }
    }

}
