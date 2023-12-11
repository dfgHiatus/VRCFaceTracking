using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using VRCFaceTracking.Contracts.Services;
using VRCFaceTracking.Models;
using VRCFaceTracking.Services;

namespace VRCFaceTracking.ViewModels;

public class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private ElementTheme _elementTheme;
    private List<GithubContributor> _contributors;

    public ElementTheme ElementTheme
    {
        get => _elementTheme;
        set => SetProperty(ref _elementTheme, value);
    }

    public ICommand SwitchThemeCommand
    {
        get;
    }

    private GithubService GithubService
    {
        get;
        set;
    }

    private OpenVRService OpenVRService
    {
        get;
    }

    private ProcessService ProcessService
    {
        get;
    }

    public List<GithubContributor> Contributors
    {
        get => _contributors;
        set => SetProperty(ref _contributors, value);
    }
    
    public bool AutoStart
    {
        get => OpenVRService.AutoStart;
        set
        {
            OpenVRService.AutoStart = value;
            OnPropertyChanged();
        }
    }

    public bool AutoClose
    {
        get => ProcessService.AutoClose;
        set
        {
            ProcessService.AutoClose = value;
            OnPropertyChanged();
        }
    }

    public bool IsOpenVREnabled => OpenVRService.IsInitialized;

    public bool HasAdmin => Utils.HasAdmin;

    private async void LoadContributors()
    {
        Contributors = await GithubService.GetContributors("benaclejames/VRCFaceTracking");
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, GithubService githubService, OpenVRService openVRService, ProcessService processService)
    {
        _themeSelectorService = themeSelectorService;
        GithubService = githubService;
        OpenVRService = openVRService;
        ProcessService = processService;

        _elementTheme = _themeSelectorService.Theme;

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });
        
        LoadContributors();
    }
}
