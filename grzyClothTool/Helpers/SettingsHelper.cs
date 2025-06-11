using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.Json;

namespace grzyClothTool.Helpers;

public class SettingsHelper : INotifyPropertyChanged
{
    private static readonly Lazy<SettingsHelper> _instance = new(() => new SettingsHelper());
    public static SettingsHelper Instance => _instance.Value;

    public event PropertyChangedEventHandler PropertyChanged;

    private bool _displaySelectedDrawablePath;
    public bool DisplaySelectedDrawablePath
    {
        get => _displaySelectedDrawablePath;
        set => SetProperty(ref _displaySelectedDrawablePath, value, nameof(DisplaySelectedDrawablePath));
    }

    private int _polygonLimitHigh;
    public int PolygonLimitHigh
    {
        get => _polygonLimitHigh;
        set => SetProperty(ref _polygonLimitHigh, value, nameof(PolygonLimitHigh), revalidateDrawables: true);
    }

    private int _polygonLimitMed;
    public int PolygonLimitMed
    {
        get => _polygonLimitMed;
        set => SetProperty(ref _polygonLimitMed, value, nameof(PolygonLimitMed), revalidateDrawables: true);
    }

    private int _polygonLimitLow;
    public int PolygonLimitLow
    {
        get => _polygonLimitLow;
        set => SetProperty(ref _polygonLimitLow, value, nameof(PolygonLimitLow), revalidateDrawables: true);
    }

    private bool _autoDeleteFiles;
    public bool AutoDeleteFiles
    {
        get => _autoDeleteFiles;
        set => SetProperty(ref _autoDeleteFiles, value, nameof(AutoDeleteFiles));
    }

    private bool _markNewDrawables;
    public bool MarkNewDrawables
    {
        get => _markNewDrawables;
        set => SetProperty(ref _markNewDrawables, value, nameof(MarkNewDrawables));
    }

    private string _renderResolution;
    public string RenderResolution
    {
        get => _renderResolution;
        set => SetProperty(ref _renderResolution, value, nameof(RenderResolution));
    }

    private string _outputResolution;
    public string OutputResolution
    {
        get => _outputResolution;
        set => SetProperty(ref _outputResolution, value, nameof(OutputResolution));
    }

    private Dictionary<string, int> _drawableTypeOffsets = new();

    private SettingsHelper()
    {
        _displaySelectedDrawablePath = Properties.Settings.Default.DisplaySelectedDrawablePath;
        _polygonLimitHigh = Properties.Settings.Default.PolygonLimitHigh;
        _polygonLimitMed = Properties.Settings.Default.PolygonLimitMed;
        _polygonLimitLow = Properties.Settings.Default.PolygonLimitLow;
        _autoDeleteFiles = Properties.Settings.Default.AutoDeleteFiles;
        _markNewDrawables = Properties.Settings.Default.MarkNewDrawables;
        _renderResolution = Properties.Settings.Default.RenderResolution;
        _outputResolution = Properties.Settings.Default.OutputResolution;
        LoadDrawableTypeOffsets();
    }

    private void LoadDrawableTypeOffsets()
    {
        try
        {
            var offsetsString = Properties.Settings.Default.DrawableTypeOffsets;
            if (!string.IsNullOrEmpty(offsetsString))
            {
                _drawableTypeOffsets = JsonSerializer.Deserialize<Dictionary<string, int>>(offsetsString) ?? new();
            }
        }
        catch
        {
            _drawableTypeOffsets = new();
        }
    }

    private void SaveDrawableTypeOffsets()
    {
        try
        {
            var offsetsString = JsonSerializer.Serialize(_drawableTypeOffsets);
            Properties.Settings.Default.DrawableTypeOffsets = offsetsString;
            Properties.Settings.Default.Save();
        }
        catch
        {
            // Handle serialization error
        }
    }

    public int GetDrawableTypeOffset(string typeName, bool isProp, Enums.SexType sex)
    {
        var key = $"{(isProp ? "prop" : "component")}_{typeName}_{sex}";
        return _drawableTypeOffsets.TryGetValue(key, out int offset) ? offset : 0;
    }

    public void SetDrawableTypeOffset(string typeName, bool isProp, Enums.SexType sex, int offset)
    {
        var key = $"{(isProp ? "prop" : "component")}_{typeName}_{sex}";
        _drawableTypeOffsets[key] = offset;
        SaveDrawableTypeOffsets();
        OnPropertyChanged(nameof(DrawableTypeOffsets));
        
        // Notify all drawables of the same type and sex to refresh their DisplayNumberWithOffset
        NotifyDrawablesOfOffsetChange(typeName, isProp, sex);
    }

    private void NotifyDrawablesOfOffsetChange(string typeName, bool isProp, Enums.SexType sex)
    {
        try
        {
            if (MainWindow.AddonManager?.Addons != null)
            {
                foreach (var addon in MainWindow.AddonManager.Addons)
                {
                    foreach (var drawable in addon.Drawables)
                    {
                        if (drawable.TypeName == typeName && drawable.IsProp == isProp && drawable.Sex == sex)
                        {
                            drawable.NotifyDisplayNumberWithOffsetChanged();
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors during notification
        }
    }

    public Dictionary<string, int> DrawableTypeOffsets => new(_drawableTypeOffsets);

    private void SetProperty<T>(ref T field, T value, string propertyName, bool revalidateDrawables = false)
    {
        if (!Equals(field, value))
        {
            field = value;
            Properties.Settings.Default[propertyName] = value;
            Properties.Settings.Default.Save();
            OnPropertyChanged(propertyName);

            if (revalidateDrawables)
            {
                foreach (var addon in MainWindow.AddonManager.Addons)
                {
                    foreach (var drawable in addon.Drawables)
                    {
                        drawable.Details.Validate();
                    }
                }
            }
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}