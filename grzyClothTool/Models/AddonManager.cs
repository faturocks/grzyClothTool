﻿using CodeWalker.GameFiles;
using grzyClothTool.Controls;
using grzyClothTool.Extensions;
using grzyClothTool.Helpers;
using grzyClothTool.Models.Drawable;
using grzyClothTool.Models.Other;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace grzyClothTool.Models
{

    public class AddonManagerDesign : AddonManager
    {
        public AddonManagerDesign()
        {
            ProjectName = "Design";
            Addons = [];

            Addons.Add(new Addon("design"));
            SelectedAddon = Addons.First();
        }
    }

    public class AddonManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ProjectName { get; set; }

        [JsonInclude]
        private string SavedAt => DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");

        [JsonIgnore]
        public bool HasProject => Addons.Count > 0;

        [JsonIgnore]
        public ObservableCollection<MoveMenuItem> MoveMenuItems { get; set; } = [];

        private ObservableCollection<Addon> _addons = [];
        public ObservableCollection<Addon> Addons
        {
            get { return _addons; }
            set
            {
                if (_addons != value)
                {
                    _addons = value;
                    OnPropertyChanged();
                }
            }
        }

        private Addon _selectedAddon;
        [JsonIgnore]
        public Addon SelectedAddon
        {
            get { return _selectedAddon; }
            set
            {
                if (_selectedAddon != value)
                {
                    _selectedAddon = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPreviewEnabled;
        [JsonIgnore]
        public bool IsPreviewEnabled
        {
            get { return _isPreviewEnabled; }
            set
            {
                _isPreviewEnabled = value;
                OnPropertyChanged();
            }
        }

        public AddonManager()
        {
        }

        public void CreateAddon()
        {
            var name = "Addon " + (Addons.Count + 1);

            Addons.Add(new Addon(name));
            OnPropertyChanged("Addons");
        }

        public async Task LoadAddon(string path, bool shouldSetProjectName = false)
        {
            var dirPath = Path.GetDirectoryName(path);
            var addonName = Path.GetFileNameWithoutExtension(path);

            // Determine if the addonName indicates male or female
            Enums.SexType sex = addonName.Contains("mp_m_freemode_01") ? Enums.SexType.male : Enums.SexType.female;

            // Build the appropriate regex pattern based on whether it's male or female
            string genderSpecificPart = sex == Enums.SexType.male ? "mp_m_freemode_01" : "mp_f_freemode_01";
            string addonNameWithoutGender = addonName.Replace(genderSpecificPart, "").TrimStart('_');

            if (shouldSetProjectName)
            {
                MainWindow.AddonManager.ProjectName = addonNameWithoutGender;
            }

            string pattern = $@"^{genderSpecificPart}(_p)?.*?{Regex.Escape(addonNameWithoutGender)}\^";

            var yddFiles = Directory.GetFiles(dirPath, "*.ydd", SearchOption.AllDirectories)
                .Where(x => Regex.IsMatch(Path.GetFileName(x), pattern, RegexOptions.IgnoreCase))
                .ToArray();

            var ymtFile = Directory.GetFiles(dirPath, "*.ymt", SearchOption.AllDirectories)
                .Where(x => x.Contains(addonName))
                .FirstOrDefault();

            var yldFiles = Directory.GetFiles(dirPath, "*.yld", SearchOption.AllDirectories)
                .Where(x => Regex.IsMatch(Path.GetFileName(x), pattern, RegexOptions.IgnoreCase))
                .ToArray();

            if (yddFiles.Length == 0)
            {
                CustomMessageBox.Show($"No .ydd files found for selected .meta file ({Path.GetFileName(path)})", "Error");
                return;
            }

            if (ymtFile == null)
            {
                CustomMessageBox.Show($"No .ymt file found for selected .meta file ({Path.GetFileName(path)})", "Error");
                return;
            }

            var ymt = new PedFile();
            RpfFile.LoadResourceFile(ymt, File.ReadAllBytes(ymtFile), 2);
            
            //merge ydd with yld files
            var mergedFiles = yddFiles.Concat(yldFiles).ToArray();

            await AddDrawables(mergedFiles, sex, ymt);
        }

        public async Task AddDrawables(string[] filePaths, Enums.SexType sex, PedFile ymt = null)
        {
            // We need to count how many drawables of each type we have added so far
            // this is because if we are loading from ymt file, numbers are relative to this ymt file, once adding it to existing project
            // we need to adjust numbers to get proper properties
            Dictionary<(int, bool), int> typeNumericCounts = [];

            //read properties from provided ymt file if there is any
            Dictionary<(int, int), MCComponentInfo> compInfoDict = [];
            Dictionary<(int, int), MCPedPropMetaData> pedPropMetaDataDict = [];
            if (ymt is not null)
            {
                var hasCompInfos = ymt.VariationInfo.CompInfos != null;
                if (hasCompInfos)
                {
                    foreach (var compInfo in ymt.VariationInfo.CompInfos)
                    {
                        var key = (compInfo.ComponentType, compInfo.ComponentIndex);
                        compInfoDict[key] = compInfo;
                    }
                }

                var hasProps = ymt.VariationInfo.PropInfo.PropMetaData != null && ymt.VariationInfo.PropInfo.Data.numAvailProps > 0;
                if (hasProps)
                {
                    foreach (var pedPropMetaData in ymt.VariationInfo.PropInfo.PropMetaData)
                    {
                        var key = (pedPropMetaData.Data.anchorId, pedPropMetaData.Data.propId);
                        pedPropMetaDataDict[key] = pedPropMetaData;
                    }
                }
            }

            Regex alternateRegex = new(@"_\w_\d+\.ydd$");
            Regex physicsRegex = new(@"\.yld$");
            
            // Pre-filter files and group them by type for better processing
            var regularDrawables = new List<(string filePath, bool isProp, int drawableType)>();
            var alternateFiles = new List<string>();
            var physicsFiles = new List<string>();

            foreach (var filePath in filePaths)
            {
                var (isProp, drawableType) = FileHelper.ResolveDrawableType(filePath);
                if (drawableType == -1)
                {
                    continue;
                }

                if (alternateRegex.IsMatch(filePath))
                {
                    alternateFiles.Add(filePath);
                    continue;
                }

                if (physicsRegex.IsMatch(filePath))
                {
                    physicsFiles.Add(filePath);
                    continue;
                }

                regularDrawables.Add((filePath, isProp, drawableType));
            }

            if(Addons.Count == 0)
            {
                CreateAddon();
            }

            // Process regular drawables concurrently in batches
            const int batchSize = 4; // Process 4 at a time to avoid overwhelming the system
            var drawableTasks = new List<Task<GDrawable>>();
            var currentAddon = Addons[0];

            for (int i = 0; i < regularDrawables.Count; i += batchSize)
            {
                var batch = regularDrawables.Skip(i).Take(batchSize);
                var batchTasks = batch.Select(async item =>
                {
                    var (filePath, isProp, drawableType) = item;
                    var drawablesOfType = currentAddon.Drawables.Where(x => x.TypeNumeric == drawableType && x.IsProp == isProp && x.Sex == sex);
                    var countOfType = drawablesOfType.Count();

                    var drawable = await Task.Run(() => FileHelper.CreateDrawableAsync(filePath, sex, isProp, drawableType, countOfType));

                    // Set properties from ymt file if available
                    if (ymt is not null)
                    {
                        // Update the dictionary with the count of the current TypeNumeric
                        var key = (drawableType, isProp);
                        lock (typeNumericCounts)
                        {
                            if (typeNumericCounts.TryGetValue(key, out int value))
                            {
                                typeNumericCounts[key] = ++value;
                            }
                            else
                            {
                                typeNumericCounts[key] = 1;
                            }
                        }

                        var ymtKey = (drawable.TypeNumeric, typeNumericCounts[(drawable.TypeNumeric, drawable.IsProp)] - 1);
                        if (compInfoDict.TryGetValue(ymtKey, out MCComponentInfo compInfo))
                        {
                            drawable.Audio = compInfo.Data.pedXml_audioID.ToString();

                            var list = EnumHelper.GetFlags((int)compInfo.Data.flags);
                            drawable.SelectedFlags = list.ToObservableCollection();

                            if (compInfo.Data.pedXml_expressionMods.f4 != 0)
                            {
                                drawable.EnableHighHeels = true;
                                drawable.HighHeelsValue = compInfo.Data.pedXml_expressionMods.f4;
                            }
                        }

                        if (drawable.IsProp)
                        {
                            if (pedPropMetaDataDict.TryGetValue(ymtKey, out MCPedPropMetaData pedPropMetaData))
                            {
                                drawable.Audio = pedPropMetaData.Data.audioId.ToString();
                                drawable.RenderFlag = pedPropMetaData.Data.renderFlags.ToString();

                                var list = EnumHelper.GetFlags((int)pedPropMetaData.Data.propFlags);
                                drawable.SelectedFlags = list.ToObservableCollection();

                                if (pedPropMetaData.Data.expressionMods.f0 != 0)
                                {
                                    drawable.EnableHairScale = true;

                                    // grzyClothTool saves hairScaleValue as positive number, on resource build it makes it negative
                                    drawable.HairScaleValue = Math.Abs(pedPropMetaData.Data.expressionMods.f0);
                                }
                            }
                        }
                    }

                    return drawable;
                });

                drawableTasks.AddRange(batchTasks);
                
                // Process batch and add drawables
                var completedDrawables = await Task.WhenAll(batchTasks);
                foreach (var drawable in completedDrawables)
                {
                    AddDrawable(drawable);
                }
            }

            // Process alternate files after main drawables are loaded
            foreach (var filePath in alternateFiles)
            {
                var (isProp, drawableType) = FileHelper.ResolveDrawableType(filePath);
                var drawablesOfType = currentAddon.Drawables.Where(x => x.TypeNumeric == drawableType && x.IsProp == isProp && x.Sex == sex);

                if (filePath.EndsWith("_1.ydd")) {
                    // Add only first alternate variation as first person file
                    var number = FileHelper.GetDrawableNumberFromFileName(Path.GetFileName(filePath));
                    if (number == null)
                    {
                        LogHelper.Log($"Could not find associated YDD file for first person file: {filePath}, please do it manually", Views.LogType.Warning);
                        continue;
                    }

                    var foundDrawable = drawablesOfType.FirstOrDefault(x => x.Number == number);
                    if (foundDrawable != null)
                    {
                        foundDrawable.FirstPersonPath = filePath;
                    }
                }
            }

            // Process physics files after main drawables are loaded
            foreach (var filePath in physicsFiles)
            {
                var (isProp, drawableType) = FileHelper.ResolveDrawableType(filePath);
                var drawablesOfType = currentAddon.Drawables.Where(x => x.TypeNumeric == drawableType && x.IsProp == isProp && x.Sex == sex);

                var number = FileHelper.GetDrawableNumberFromFileName(Path.GetFileName(filePath));
                if (number == null)
                {
                    LogHelper.Log($"Could not find associated YDD file for this YLD: {filePath}, please do it manually", Views.LogType.Warning);
                    continue;
                }

                var foundDrawable = drawablesOfType.FirstOrDefault(x => x.Number == number);
                if (foundDrawable != null)
                {
                    foundDrawable.ClothPhysicsPath = filePath;
                }
            }

            Addons.Sort(true);
        }

        public void AddDrawable(GDrawable drawable)
        {
            int nextNumber = 0;
            int currentAddonIndex = 0;
            Addon currentAddon;

            // find to which addon we should add the drawable
            while (currentAddonIndex < Addons.Count)
            {
                currentAddon = Addons[currentAddonIndex];
                int countOfType = currentAddon.Drawables.Count(x => x.TypeNumeric == drawable.TypeNumeric && x.IsProp == drawable.IsProp && x.Sex == drawable.Sex);

                // If the number of drawables of this type has reached 128, move to the next addon
                if (countOfType >= GlobalConstants.MAX_DRAWABLES_IN_ADDON)
                {
                    currentAddonIndex++;
                    continue;
                }

                nextNumber = countOfType;
                break;
            }

            // make sure we are adding to correct addon
            if (currentAddonIndex < Addons.Count)
            {
                currentAddon = Addons[currentAddonIndex];
            }
            else
            {
                // Create a new Addon
                currentAddon = new Addon("Addon " + (currentAddonIndex + 1));
                Addons.Add(currentAddon);
            }

            // Update name and number
            // mark as new, to make it easier to find
            drawable.IsNew = true;
            drawable.Number = nextNumber;
            drawable.SetDrawableName();

            currentAddon.Drawables.Add(drawable);

            SaveHelper.SetUnsavedChanges(true);
        }

        public void DeleteDrawables(List<GDrawable> drawables)
        {
            DeleteDrawables(drawables, false);
        }

        public void DeleteDrawables(List<GDrawable> drawables, bool searchAcrossAllAddons)
        {
            SaveHelper.SetUnsavedChanges(true);
            
            if (searchAcrossAllAddons)
            {
                // Group drawables by their containing addon for efficient processing
                var drawablesByAddon = new Dictionary<Addon, List<GDrawable>>();
                var drawablesNotFound = new List<GDrawable>();
                
                foreach (var drawable in drawables)
                {
                    var containingAddon = Addons.FirstOrDefault(a => a.Drawables.Contains(drawable));
                    if (containingAddon != null)
                    {
                        if (!drawablesByAddon.ContainsKey(containingAddon))
                        {
                            drawablesByAddon[containingAddon] = new List<GDrawable>();
                        }
                        drawablesByAddon[containingAddon].Add(drawable);
                    }
                    else
                    {
                        drawablesNotFound.Add(drawable);
                    }
                }
                
                // Log any drawables that couldn't be found
                if (drawablesNotFound.Count > 0)
                {
                    LogHelper.Log($"Warning: {drawablesNotFound.Count} drawable(s) were not found in any addon and could not be deleted.", Views.LogType.Warning);
                }
                
                // Remove drawables from their respective addons
                var addonsToRemove = new List<Addon>();
                int totalDeleted = 0;
                
                foreach (var kvp in drawablesByAddon)
                {
                    var addon = kvp.Key;
                    var addonDrawables = kvp.Value;
                    
                    foreach (var drawable in addonDrawables)
                    {
                        if (addon.Drawables.Remove(drawable))
                        {
                            totalDeleted++;
                            
                            if (SettingsHelper.Instance.AutoDeleteFiles)
                            {
                                try
                                {
                                    foreach (var texture in drawable.Textures)
                                    {
                                        if (File.Exists(texture.FilePath))
                                        {
                                            File.Delete(texture.FilePath);
                                        }
                                    }
                                    if (File.Exists(drawable.FilePath))
                                    {
                                        File.Delete(drawable.FilePath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.Log($"Warning: Could not delete files for drawable {drawable.Name}: {ex.Message}", Views.LogType.Warning);
                                }
                            }
                        }
                    }
                    
                    // Sort the addon after removal
                    if (addon.Drawables.Count > 0)
                    {
                        addon.Drawables.Sort(true);
                    }
                    else
                    {
                        // Mark empty addons for removal
                        addonsToRemove.Add(addon);
                    }
                }
                
                // Remove empty addons
                foreach (var addon in addonsToRemove)
                {
                    DeleteAddon(addon);
                }
                
                LogHelper.Log($"Successfully deleted {totalDeleted} drawable(s) across {drawablesByAddon.Count} addon(s).", Views.LogType.Info);
            }
            else
            {
                // Original logic: only delete from selected addon
                var addon = SelectedAddon;
                foreach (GDrawable drawable in drawables)
                {
                    addon.Drawables.Remove(drawable);

                    if (SettingsHelper.Instance.AutoDeleteFiles)
                    {
                        foreach (var texture in drawable.Textures)
                        {
                            File.Delete(texture.FilePath);
                        }
                        File.Delete(drawable.FilePath);
                    }
                }

                // if addon is empty, remove it
                if (addon.Drawables.Count == 0)
                {
                    DeleteAddon(addon);
                    return;
                }

                addon.Drawables.Sort(true);
            }
        }

        public void MoveDrawable(GDrawable drawable, Addon targetAddon)
        {
            if (drawable == null || targetAddon == null)
            {
                var nullParam = drawable == null ? nameof(drawable) : nameof(targetAddon);
                throw new ArgumentNullException(nullParam, $"{nullParam} cannot be null.");
            }

            var currentAddon = Addons.FirstOrDefault(a => a.Drawables.Contains(drawable));
            if (currentAddon != null)
            {
                currentAddon.Drawables.Remove(drawable);

                drawable.Number = currentAddon.GetNextDrawableNumber(drawable.TypeNumeric, drawable.IsProp, drawable.Sex);
                drawable.SetDrawableName();

                targetAddon.Drawables.Add(drawable);
            }
        }

        public int GetTotalDrawableAndTextureCount()
        {
            return Addons.Sum(addon => addon.GetTotalDrawableAndTextureCount());
        }

        private void DeleteAddon(Addon addon)
        {
            if (Addons.Count <= 1)
            {
                return;
            }

            int index = Addons.IndexOf(addon);
            if (index < 0) { return; } // if not found, don't remove

            Addons.RemoveAt(index);
            AdjustAddonNames();
        }

        private void AdjustAddonNames()
        {
            for (int i = 0; i < Addons.Count; i++)
            {
                Addons[i].Name = $"Addon {i + 1}";
            }

            OnPropertyChanged("Addons");
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
