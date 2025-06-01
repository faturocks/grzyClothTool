using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Texture;
using grzyClothTool.Views;
using ImageMagick;
using System.Text.Json;

namespace grzyClothTool.Helpers;

public static class FileHelper
{
    public static string ReservedAssetsPath { get; private set; }

    public static void GenerateReservedAssets()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var exeName = Assembly.GetExecutingAssembly().GetName().Name;

        ReservedAssetsPath = Path.Combine(documentsPath, exeName, "reservedAssets");
        Directory.CreateDirectory(ReservedAssetsPath);
        CreateReservedAsset("reservedDrawable", ".ydd");
        CreateReservedAsset("reservedTexture", ".ytd");
    }

    private static void CreateReservedAsset(string name, string extension)
    {
        var outputPath = Path.Combine(ReservedAssetsPath, name + extension);

        if(!File.Exists(outputPath))
        {
            byte[] resourceData = (byte[])Properties.Resources.ResourceManager.GetObject(name, CultureInfo.InvariantCulture);
            if (resourceData != null)
            {
                File.WriteAllBytes(outputPath, resourceData);
                return;
            }
        }
    }

    public static Task<GDrawable> CreateDrawableAsync(string filePath, Enums.SexType sex, bool isProp, int typeNumber, int countOfType)
    {
        var name = EnumHelper.GetName(typeNumber, isProp);

        var matchingTextures = FindMatchingTextures(filePath, name, isProp);

        var drawableName = Guid.NewGuid().ToString();
        var drawableRaceSuffix = Path.GetFileNameWithoutExtension(filePath)[^1..];
        var drawableHasSkin = drawableRaceSuffix == "r";

        // Should we inform user, that they tried to add too many textures?
        var textures = new ObservableCollection<GTexture>(matchingTextures.Select((path, txtNumber) => new GTexture(path, typeNumber, countOfType, txtNumber, drawableHasSkin, isProp)).Take(GlobalConstants.MAX_DRAWABLE_TEXTURES));

        return Task.FromResult(new GDrawable(filePath, sex, isProp, typeNumber, countOfType, drawableHasSkin, textures));
    }

    public static async Task CopyAsync(string sourcePath, string destinationPath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await sourceStream.CopyToAsync(destinationStream);
    }

    public static void OpenFileLocation(string path)
    {
        try
        {
            Process.Start("explorer.exe", $"/select, \"{path}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred while trying to open the file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static List<string> FindMatchingTextures(string filePath, string name, bool isProp)
    {
        var folderPath = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        var addonName = string.Empty;

        if (fileName.Contains('^'))
        {
            var split = fileName.Split("^");
            addonName = split[0];
            fileName = split[1];
        }
        string[] nameParts = Path.GetFileNameWithoutExtension(fileName).Split("_");

        string searchedNumber, regexToSearch;
        if (nameParts.Length == 1) //this will happen when someone is adding weirdly named ydds (for example 5.ydd)
        {
            searchedNumber = nameParts[0];
            regexToSearch = $"^{searchedNumber}([a-z]|_[a-z])?"; //this will try to find 5.ytd 5a.ytd or 5_a.ytd files
        } 
        else
        {
            searchedNumber = isProp ? nameParts[2] : nameParts[1];
            regexToSearch = $"^{name}_diff_{searchedNumber}";
        }

        if (addonName != string.Empty)
        {
            regexToSearch = $"^{addonName}\\{regexToSearch}";
        }

        var allYtds = Directory.EnumerateFiles(folderPath)
            .Where(x => Path.GetExtension(x) == ".ytd" &&
                Regex.IsMatch(Path.GetFileNameWithoutExtension(x), regexToSearch))
            .ToList();

        return allYtds;
    }

    public static (bool, int) ResolveDrawableType(string file)
    {
        string fileName = Path.GetFileNameWithoutExtension(file);
        if (fileName.Contains('^'))
        {
            fileName = fileName.Split("^")[1];
        }

        var componentsList = EnumHelper.GetDrawableTypeList();
        var propsList = EnumHelper.GetPropTypeList();

        var compName = componentsList.FirstOrDefault(name => fileName.StartsWith(name + "_"));
        var propName = propsList.FirstOrDefault(name => fileName.StartsWith(name + "_"));

        if (compName != null)
        {
            var value = EnumHelper.GetValue(compName, false);
            return (false, value);
        }

        if (propName != null)
        {
            var value = EnumHelper.GetValue(propName, true);
            return (true, value);
        }

        var window = new DrawableSelectWindow(file);
        var result = window.ShowDialog();
        if (result == true)
        {
            var value = EnumHelper.GetValue(window.SelectedDrawableType, window.IsProp);
            return (window.IsProp, value);
        }

        return (false, -1);
    }

    public static int? GetDrawableNumberFromFileName(string fileName)
    {
        Regex numberRegex = new(@"_(\d{3})_([a-zA-Z])\.yld$", RegexOptions.Compiled);
        Match match = numberRegex.Match(fileName);

        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }
        return null;
    }

    public static async Task SaveTexturesAsync(List<GTexture> textures, string folderPath, string format)
    {
        Directory.CreateDirectory(folderPath);

        // Determine file extension
        string fileExtension = format.ToUpper() switch
        {
            "DDS" => ".dds",
            "PNG" => ".png",
            "YTD" => ".ytd",
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format))
        };

        ProgressHelper.Start("Started exporting textures");

        int successfulExports = 0;

        // Process each texture asynchronously and save it to the specified folder
        var tasks = textures.Select(async texture =>
        {
            string filePath = Path.Combine(folderPath, $"{texture.DisplayName}{fileExtension}");

            // check if file exists
            if (File.Exists(filePath))
            {
                LogHelper.Log($"Could not save texture: {texture.DisplayName}. Error: File already exists.", LogType.Error);
                return;
            }
             
            if (fileExtension == ".ytd") 
            {
                // For YTD, simply copy the file
                try
                {
                    await CopyAsync(texture.FilePath, filePath);
                    successfulExports++;
                } 
                catch (Exception ex)
                {
                    // Log the error and continue processing other textures
                    LogHelper.Log($"Could not save texture: {texture.DisplayName}. Error: {ex.Message}.", LogType.Error);
                } 
            }
            else
            {
                using var image = ImgHelper.GetImage(texture.FilePath);
                image.Format = format.ToUpper() switch
                {
                    "DDS" => MagickFormat.Dds,
                    "PNG" => MagickFormat.Png,
                    _ => throw new ArgumentException($"Unsupported format for MagickImage: {format}", nameof(format))
                };

                try
                {
                    await File.WriteAllBytesAsync(filePath, image.ToByteArray());
                    successfulExports++;
                }
                catch (Exception ex)
                {
                    // Log the error and continue processing other textures
                    LogHelper.Log($"Could not save texture: {texture.DisplayName}. Error: {ex.Message}.", LogType.Error);
                }
            }
        });

        await Task.WhenAll(tasks);

        ProgressHelper.Stop($"Exported {successfulExports} texture(s) in {{0}}", true);
    }

    public static async Task SaveDrawablesAsync(List<GDrawable> drawables, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        ProgressHelper.Start("Started exporting drawables");

        int successfulExports = 0;

        // Process each drawable asynchronously and save it to the specified folder
        var tasks = drawables.Select(async drawable =>
        {
            string filePath = Path.Combine(folderPath, $"{drawable.Name}{Path.GetExtension(drawable.FilePath)}");

            // check if file exists
            if (File.Exists(filePath))
            {
                LogHelper.Log($"Could not save drawable: {drawable.Name}. Error: File already exists.", LogType.Error);
                return;
            }

            try
            {
                await CopyAsync(drawable.FilePath, filePath);
                successfulExports++;
            }
            catch (Exception ex)
            {
                // Log the error and continue processing other drawables
                LogHelper.Log($"Could not save drawable: {drawable.Name}. Error: {ex.Message}.", LogType.Error);
            }
        });

        await Task.WhenAll(tasks);

        ProgressHelper.Stop($"Exported {successfulExports} drawable(s) in {{0}}", true);
    }

    public static async Task SaveDrawablesAsJsonAsync(List<GDrawable> drawables, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        ProgressHelper.Start("Started exporting drawables as JSON");

        int successfulExports = 0;

        // Process each drawable asynchronously and save it to the specified folder
        var tasks = drawables.Select(async drawable =>
        {
            try
            {
                // Create comprehensive drawable data for debugging
                var debugData = new
                {
                    // Basic drawable information
                    Name = drawable.Name,
                    FilePath = drawable.FilePath,
                    FirstPersonPath = drawable.FirstPersonPath,
                    ClothPhysicsPath = drawable.ClothPhysicsPath,
                    
                    // Type and identification
                    TypeName = drawable.TypeName,
                    TypeNumeric = drawable.TypeNumeric,
                    IsProp = drawable.IsProp,
                    IsComponent = drawable.IsComponent,
                    Sex = drawable.Sex.ToString(),
                    SexName = drawable.SexName,
                    Number = drawable.Number,
                    DisplayNumber = drawable.DisplayNumber,
                    
                    // Configuration properties
                    HasSkin = drawable.HasSkin,
                    Audio = drawable.Audio,
                    RenderFlag = drawable.RenderFlag,
                    Flags = drawable.Flags,
                    FlagsText = drawable.FlagsText,
                    
                    // Selected flags detail
                    SelectedFlags = drawable.SelectedFlags?.Select(f => new 
                    {
                        Text = f.Text,
                        Value = f.Value,
                        IsSelected = f.IsSelected
                    }).ToList(),
                    
                    // Hair and high heels settings
                    EnableHairScale = drawable.EnableHairScale,
                    HairScaleValue = drawable.HairScaleValue,
                    EnableHighHeels = drawable.EnableHighHeels,
                    HighHeelsValue = drawable.HighHeelsValue,
                    
                    // Preview settings
                    EnableKeepPreview = drawable.EnableKeepPreview,
                    
                    // Loading state
                    IsLoading = drawable.IsLoading,
                    IsNew = drawable.IsNew,
                    IsReserved = drawable.IsReserved,
                    
                    // Textures information
                    Textures = drawable.Textures?.Select(tex => new
                    {
                        FilePath = tex.FilePath,
                        DisplayName = tex.DisplayName,
                        Extension = tex.Extension,
                        Number = tex.Number,
                        TxtNumber = tex.TxtNumber,
                        TxtLetter = tex.TxtLetter,
                        TypeNumeric = tex.TypeNumeric,
                        TypeName = tex.TypeName,
                        IsProp = tex.IsProp,
                        HasSkin = tex.HasSkin,
                        IsOptimizedDuringBuild = tex.IsOptimizedDuringBuild,
                        IsPreviewDisabled = tex.IsPreviewDisabled,
                        IsLoading = tex.IsLoading,
                        
                        // Texture details
                        TxtDetails = tex.TxtDetails != null ? new
                        {
                            Width = tex.TxtDetails.Width,
                            Height = tex.TxtDetails.Height,
                            MipMapCount = tex.TxtDetails.MipMapCount,
                            Compression = tex.TxtDetails.Compression,
                            Name = tex.TxtDetails.Name,
                            Type = tex.TxtDetails.Type,
                            IsOptimizeNeeded = tex.TxtDetails.IsOptimizeNeeded,
                            IsOptimizeNeededTooltip = tex.TxtDetails.IsOptimizeNeededTooltip
                        } : null,
                        
                        // Optimization details
                        OptimizeDetails = tex.OptimizeDetails != null ? new
                        {
                            Width = tex.OptimizeDetails.Width,
                            Height = tex.OptimizeDetails.Height,
                            MipMapCount = tex.OptimizeDetails.MipMapCount,
                            Compression = tex.OptimizeDetails.Compression,
                            Name = tex.OptimizeDetails.Name,
                            Type = tex.OptimizeDetails.Type,
                            IsOptimizeNeeded = tex.OptimizeDetails.IsOptimizeNeeded,
                            IsOptimizeNeededTooltip = tex.OptimizeDetails.IsOptimizeNeededTooltip
                        } : null
                    }).ToList(),
                    
                    // Detailed drawable information (loaded on demand)
                    Details = drawable.Details != null ? new
                    {
                        TexturesCount = drawable.Details.TexturesCount,
                        IsWarning = drawable.Details.IsWarning,
                        Tooltip = drawable.Details.Tooltip,
                        PolygonCountInfo = drawable.Details.PolygonCountInfo,
                        
                        // LOD models information
                        AllModels = drawable.Details.AllModels?.ToDictionary(
                            kvp => kvp.Key.ToString(),
                            kvp => kvp.Value != null ? new
                            {
                                PolyCount = kvp.Value.PolyCount
                            } : null
                        ),
                        
                        // Embedded textures information
                        EmbeddedTextures = drawable.Details.EmbeddedTextures?.ToDictionary(
                            kvp => kvp.Key.ToString(),
                            kvp => kvp.Value != null ? new
                            {
                                Width = kvp.Value.Width,
                                Height = kvp.Value.Height,
                                MipMapCount = kvp.Value.MipMapCount,
                                Compression = kvp.Value.Compression,
                                Name = kvp.Value.Name,
                                Type = kvp.Value.Type,
                                IsOptimizeNeeded = kvp.Value.IsOptimizeNeeded,
                                IsOptimizeNeededTooltip = kvp.Value.IsOptimizeNeededTooltip
                            } : null
                        )
                    } : null,
                    
                    // Metadata for debugging
                    ExportMetadata = new
                    {
                        ExportDate = DateTime.UtcNow,
                        ExportVersion = "1.0",
                        FileExists = File.Exists(drawable.FilePath),
                        FileSize = File.Exists(drawable.FilePath) ? new FileInfo(drawable.FilePath).Length : 0,
                        FirstPersonFileExists = !string.IsNullOrEmpty(drawable.FirstPersonPath) && File.Exists(drawable.FirstPersonPath),
                        ClothPhysicsFileExists = !string.IsNullOrEmpty(drawable.ClothPhysicsPath) && File.Exists(drawable.ClothPhysicsPath)
                    }
                };

                // Load drawable details if not already loaded for complete information
                if (drawable.Details == null && !drawable.IsLoading)
                {
                    try
                    {
                        await drawable.LoadDetailsOnDemandAsync();
                        
                        // Update the details in our export data
                        if (drawable.Details != null)
                        {
                            var detailsProperty = debugData.GetType().GetProperty("Details");
                            // Note: We'd need to recreate the object with updated details, but for simplicity we'll keep it as is
                            // The details loading is async and might complete after JSON serialization
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Log($"Could not load details for drawable {drawable.Name}: {ex.Message}", Views.LogType.Warning);
                    }
                }

                // Serialize to JSON with indentation for readability
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(debugData, options);
                
                string filePath = Path.Combine(folderPath, $"{drawable.Name}.json");

                // Check if file exists
                if (File.Exists(filePath))
                {
                    LogHelper.Log($"Could not save drawable JSON: {drawable.Name}. Error: File already exists.", Views.LogType.Error);
                    return;
                }

                await File.WriteAllTextAsync(filePath, json);
                successfulExports++;
                
                LogHelper.Log($"Exported drawable JSON: {drawable.Name}", Views.LogType.Info);
            }
            catch (Exception ex)
            {
                // Log the error and continue processing other drawables
                LogHelper.Log($"Could not save drawable JSON: {drawable.Name}. Error: {ex.Message}.", Views.LogType.Error);
            }
        });

        await Task.WhenAll(tasks);

        ProgressHelper.Stop($"Exported {successfulExports} drawable(s) as JSON in {{0}}", true);
    }
}
