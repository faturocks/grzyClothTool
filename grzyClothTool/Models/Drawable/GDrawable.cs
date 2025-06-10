using grzyClothTool.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using CodeWalker.GameFiles;
using System.Linq;
using System;
using grzyClothTool.Controls;
using System.Runtime.Serialization;
using grzyClothTool.Models.Texture;
using grzyClothTool.Extensions;
using System.Collections.Specialized;
using System.Text.Json.Serialization;

namespace grzyClothTool.Models.Drawable;
#nullable enable

public class GDrawable : INotifyPropertyChanged
{
    private readonly static SemaphoreSlim _semaphore = new(3);

    public event PropertyChangedEventHandler PropertyChanged;

    private string _filePath;
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isNew;
    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (_isNew != value)
            {
                _isNew = value;
                OnPropertyChanged();
            }
        }
    }

    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public virtual bool IsReserved => false;

    public int TypeNumeric { get; set; }
    private string _typeName;
    public string TypeName
    {
        get
        {
            _typeName ??= EnumHelper.GetName(TypeNumeric, IsProp);
            return _typeName;
        }
        set
        {
            _typeName = value;

            //TypeNumeric = EnumHelper.GetValue(value, IsProp);

            SetDrawableName();
            OnPropertyChanged();
        }
    }

    private string _sexName;
    public string SexName
    {
        get
        {
            return _sexName ??= Enum.GetName(Sex)!;
        }
        set
        {
            _sexName = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public List<string> AvailableTypes => IsProp ? EnumHelper.GetPropTypeList() : EnumHelper.GetDrawableTypeList();

    [JsonIgnore]
    public static List<string> AvailableSex => EnumHelper.GetSexTypeList();

    private Enums.SexType _sex;
    public Enums.SexType Sex
    {
        get => _sex;
        set
        {
            _sex = value;
            OnPropertyChanged();
        }
    }

    public bool IsProp { get; set; }
    public bool IsComponent => !IsProp;

    public int Number { get; set; }
    public string DisplayNumber => (Number % GlobalConstants.MAX_DRAWABLES_IN_ADDON).ToString("D3");

    public string DisplayNumberWithOffset
    {
        get
        {
            try
            {
                // Get offset for this drawable type and sex
                var offset = SettingsHelper.Instance.GetDrawableTypeOffset(TypeName, IsProp, Sex);
                
                // Calculate count from previous addons of the same type
                var previousAddonsCount = 0;
                
                // Safety check for MainWindow.AddonManager
                if (MainWindow.AddonManager?.Addons != null && MainWindow.AddonManager.SelectedAddon != null)
                {
                    var currentAddonIndex = MainWindow.AddonManager.Addons.IndexOf(MainWindow.AddonManager.SelectedAddon);
                    
                    for (int i = 0; i < currentAddonIndex; i++)
                    {
                        var addon = MainWindow.AddonManager.Addons[i];
                        previousAddonsCount += addon.Drawables.Count(d => 
                            d.TypeNumeric == TypeNumeric && 
                            d.IsProp == IsProp && 
                            d.Sex == Sex);
                    }
                }
                
                // Calculate final number: Offset + Previous addons count + Current number + 1
                var finalNumber = offset + previousAddonsCount + Number + 1;
                
                return finalNumber.ToString("D3");
            }
            catch
            {
                // Fallback to regular display number if there's any error
                return DisplayNumber;
            }
        }
    }

    private GDrawableDetails _details;
    public GDrawableDetails Details
    {
        get 
        {
            // For deserialized drawables, don't trigger automatic loading
            // Loading will be handled manually when needed (e.g., on hover)
            return _details;
        }
        set
        {
            if (_details != value)
            {
                _details = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    private bool _detailsLoadingStarted = false;
    
    // Public method to load details when explicitly requested
    public async Task<GDrawableDetails> LoadDetailsOnDemandAsync()
    {
        // If already loaded, return immediately
        if (_details != null) return _details;
        
        // If currently loading, wait for completion
        if (_detailsLoadingStarted)
        {
            // Wait until loading is complete
            while (_detailsLoadingStarted && _details == null)
            {
                await Task.Delay(50);
            }
            return _details;
        }
        
        return await LoadDetailsAsync();
    }
    
    private async Task<GDrawableDetails> LoadDetailsAsync()
    {
        // Prevent multiple simultaneous loads
        if (_detailsLoadingStarted) return _details;
        _detailsLoadingStarted = true;
        
        try
        {
            IsLoading = true;
            var details = await LoadDrawableDetailsWithConcurrencyControl();
            if (details != null)
            {
                Details = details;
            }
            return details;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load details for {Name}: {ex.Message}");
            return null;
        }
        finally
        {
            IsLoading = false;
            _detailsLoadingStarted = false;
        }
    }

    public string? FirstPersonPath { get; set; } = null;
    public string? ClothPhysicsPath { get; set; } = null;


    private bool _hasSkin;
    public bool HasSkin
    {
        get { return _hasSkin; }
        set
        {
            if (_hasSkin != value)
            {
                _hasSkin = value;

                foreach (var txt in Textures)
                {
                    txt.HasSkin = value;
                }
                SetDrawableName();
                OnPropertyChanged();
            }
        }
    }

    private bool _enableKeepPreview;
    public bool EnableKeepPreview
    {
        get => _enableKeepPreview;
        set { _enableKeepPreview = value; OnPropertyChanged(); }
    }

    public float HairScaleValue { get; set; } = 0.5f;


    private bool _enableHairScale;
    public bool EnableHairScale
    {
        get => _enableHairScale;
        set { _enableHairScale = value; OnPropertyChanged(); }
    }

    public float HighHeelsValue { get; set; } = 1.0f;
    private bool _enableHighHeels;
    public bool EnableHighHeels
    {
        get => _enableHighHeels;
        set { _enableHighHeels = value; OnPropertyChanged(); }
    }

    private string _audio;
    public string Audio
    {
        get => _audio;
        set
        {
            _audio = value;
            OnPropertyChanged();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    [JsonIgnore]
    public List<string> AvailableAudioList => EnumHelper.GetAudioList(TypeNumeric);

    private ObservableCollection<SelectableItem> _selectedFlags = [];
    public ObservableCollection<SelectableItem> SelectedFlags
    {
        get => _selectedFlags;
        set
        {
            _selectedFlags = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Flags));
            OnPropertyChanged(nameof(FlagsText));
        }
    }

    [JsonIgnore]
    public string FlagsText
    {
        get
        {
            var count = SelectedFlags.Count(i => i.IsSelected && i.Value != (int)Enums.DrawableFlags.NONE);

            return count > 0 ? $"{Flags} ({count} selected)" : "NONE";
        } 
    }

    [JsonIgnore]
    public int Flags => SelectedFlags.Where(f => f.IsSelected).Sum(f => f.Value);

    [JsonIgnore]
    public List<SelectableItem> AvailableFlags => EnumHelper.GetFlags(Flags);

    public string RenderFlag { get; set; } = ""; // "" is the default value

    [JsonIgnore]
    public static List<string> AvailableRenderFlagList => ["", "PRF_ALPHA", "PRF_DECAL", "PRF_CUTOUT"];

    public ObservableCollection<Texture.GTexture> Textures { get; set; }

    public GDrawable(string filePath, Enums.SexType sex, bool isProp, int typeNumeric, int number, bool hasSkin, ObservableCollection<GTexture> textures)
    {
        IsLoading = true;

        FilePath = filePath;
        Textures = textures;
        Textures.CollectionChanged += OnTexturesCollectionChanged;
        TypeNumeric = typeNumeric;
        Number = number;
        HasSkin = hasSkin;
        Sex = sex;
        IsProp = isProp;
        IsNew = true;

        Audio = "none";
        SetDrawableName();

        if (FilePath != null)
        {
            // Only start loading details if this is a new drawable being created
            // For deserialized drawables (from project load), details will be loaded lazily when needed
            if (IsNew)
            {
                Task<GDrawableDetails?> _drawableDetailsTask = LoadDrawableDetailsWithConcurrencyControl().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Console.WriteLine(t.Exception);
                        //todo: add some warning that it couldn't load
                        IsLoading = true;
                        return null;
                    }

                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        if (t.Result == null)
                        {
                            return null;
                        }

                        Details = t.Result;
                        OnPropertyChanged(nameof(Details));
                        IsLoading = false;
                    }

                    return t.Result;
                });
            }
            else
            {
                // For deserialized drawables, just mark as not loading
                // Details will be loaded on-demand when accessed
                IsLoading = false;
            }
        }
    }

    protected GDrawable(Enums.SexType sex, bool isProp, int compType, int count) { /* Used in GReservedDrawable */ }

    public void SetDrawableName()
    {
        string name = $"{TypeName}_{DisplayNumber}";
        var finalName = IsProp ? name : $"{name}_{(HasSkin ? "r" : "u")}";

        Name = finalName;
        //texture number needs to be updated too
        foreach (var txt in Textures)
        {
            txt.Number = Number;
            txt.TypeNumeric = TypeNumeric;
        }

        OnPropertyChanged(nameof(Name));
    }

    public void ChangeDrawableType(string newType)
    {
        var newTypeNumeric = EnumHelper.GetValue(newType, IsProp);
        var reserved = new GDrawableReserved(Sex, IsProp, TypeNumeric, Number);
        var index = MainWindow.AddonManager.SelectedAddon.Drawables.IndexOf(this);

        // change drawable to new type
        TypeNumeric = newTypeNumeric;

        // replace drawable with reserved in the same place
        MainWindow.AddonManager.SelectedAddon.Drawables[index] = reserved;

        // re-add changed drawable
        MainWindow.AddonManager.AddDrawable(this);
        MainWindow.AddonManager.Addons.Sort(true);
    }

    public void ChangeDrawableSex(string newSex)
    {
        // transform new sex to enum
        var newSexEnum = Enum.Parse<Enums.SexType>(newSex);
        var reserved = new GDrawableReserved(Sex, IsProp, TypeNumeric, Number);
        var index = MainWindow.AddonManager.SelectedAddon.Drawables.IndexOf(this);
    
        // change drawable sex
        Sex = newSexEnum;

        // replace drawable with reserved in the same place
        MainWindow.AddonManager.SelectedAddon.Drawables[index] = reserved;

        // re-add changed drawable
        MainWindow.AddonManager.AddDrawable(this);
        MainWindow.AddonManager.Addons.Sort(true);
    }

    private void OnTexturesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Details == null)
        {
            return;
        }

        Details.TexturesCount = Textures.Count;
        Details.Validate();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void NotifyDisplayNumberWithOffsetChanged()
    {
        OnPropertyChanged(nameof(DisplayNumberWithOffset));
    }

    private async Task<GDrawableDetails?> LoadDrawableDetailsWithConcurrencyControl()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await GetDrawableDetailsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<GDrawableDetails?> GetDrawableDetailsAsync()
    {
        var bytes = await File.ReadAllBytesAsync(FilePath);

        var yddFile = new YddFile();
        await yddFile.LoadAsync(bytes);

        if (yddFile.DrawableDict.Drawables.Count == 0)
        {
            return null;
        }

        GDrawableDetails details = new();


        //is it always 2 and 3?
        var spec = (yddFile.Drawables.First().ShaderGroup.Shaders.data_items.First().ParametersList.Parameters[3].Data as CodeWalker.GameFiles.Texture);
        var normal = (yddFile.Drawables.First().ShaderGroup.Shaders.data_items.First().ParametersList.Parameters[2].Data as CodeWalker.GameFiles.Texture);

        foreach (GDrawableDetails.EmbeddedTextureType txtType in Enum.GetValues(typeof(GDrawableDetails.EmbeddedTextureType)))
        {
            var texture = txtType switch
            {
                GDrawableDetails.EmbeddedTextureType.Specular => spec,
                GDrawableDetails.EmbeddedTextureType.Normal => normal,
                _ => null
            };

            if (texture == null)
            {
                continue;
            }

            details.EmbeddedTextures[txtType] = new GTextureDetails
            {
                Width = texture.Width,
                Height = texture.Height,
                Name = texture.Name,
                Type = txtType.ToString(),
                MipMapCount = texture.Levels,
                Compression = texture.Format.ToString()
            };
        }

        var drawableModels = yddFile.Drawables.First().DrawableModels;
        foreach (GDrawableDetails.DetailLevel detailLevel in Enum.GetValues(typeof(GDrawableDetails.DetailLevel)))
        {
            var model = detailLevel switch
            {
                GDrawableDetails.DetailLevel.High => drawableModels.High,
                GDrawableDetails.DetailLevel.Med => drawableModels.Med,
                GDrawableDetails.DetailLevel.Low => drawableModels.Low,
                _ => null
            };

            if (model != null)
            {
                details.AllModels[detailLevel] = new GDrawableModel
                {
                    PolyCount = (int)model.Sum(y => y.Geometries.Sum(g => g.IndicesCount / 3))
                };
            }
        }

        details.TexturesCount = Textures.Count;

        details.Validate();
        return details;
    }
}
