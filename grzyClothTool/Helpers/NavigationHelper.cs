using grzyClothTool.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace grzyClothTool.Helpers;
public class NavigationHelper : INotifyPropertyChanged
{
    private ObservableCollection<SaveFile> _saveFiles = [];
    public ObservableCollection<SaveFile> SaveFiles
    {
        get { return _saveFiles; }
        set
        {
            _saveFiles = value;
            OnPropertyChanged(nameof(SaveFiles));
        }
    }


    private readonly Dictionary<string, Func<UserControl>> _pageFactories = [];
    private readonly Dictionary<string, UserControl> _pages = [];

    public event PropertyChangedEventHandler PropertyChanged;

    private UserControl _currentPage;
    public UserControl CurrentPage
    {
        get { return _currentPage; }
        set
        {
            _currentPage = value;
            OnPropertyChanged(nameof(CurrentPage));
        }
    }

    private string _previousPage;
    public string PreviousPage
    {
        get { return _previousPage; }
        set
        {
            _previousPage = value;
            OnPropertyChanged(nameof(PreviousPage));
        }
    }

    public NavigationHelper()
    {
        CurrentPage = new ProjectWindow();

        SaveFiles = SaveHelper.GetSaveFiles();
        SaveHelper.SaveCreated += () => SaveFiles = SaveHelper.GetSaveFiles();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void RegisterPage(string pageKey, Func<UserControl> pageFactory)
    {
        _pageFactories.TryAdd(pageKey, pageFactory);
    }

    public void Navigate(string pageKey)
    {
        if (_pageFactories.TryGetValue(pageKey, out var pageFactory))
        {
            // Store the current page as previous before navigating
            if (_currentPage != null)
            {
                // Find the key for the current page
                foreach (var kvp in _pages)
                {
                    if (kvp.Value == _currentPage)
                    {
                        // Only update PreviousPage if we're not navigating to the same page
                        if (kvp.Key != pageKey)
                        {
                            PreviousPage = kvp.Key;
                        }
                        break;
                    }
                }
            }

            if (!_pages.TryGetValue(pageKey, out UserControl page))
            {
                page = pageFactory.Invoke();
                _pages.Add(pageKey, page);
            }

            // Update the current page before setting content
            CurrentPage = page;
            MainWindow.Instance.MainWindowContentControl.Content = page;
        }
    }

    public void NavigateBack()
    {
        // If there's a previous page, navigate to it
        if (!string.IsNullOrEmpty(PreviousPage))
        {
            Navigate(PreviousPage);
        }
        else
        {
            // Fallback to Home if no previous page is stored
            Navigate("Home");
        }
    }
}
