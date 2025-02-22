using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Core.Models;

public enum InstallState
{
    NotInstalled,
    Installed,
    Outdated,
    AwaitingRestart
}

public class InstallableTrackingModule : TrackingModuleMetadata, INotifyPropertyChanged
{
    public bool IsInstalled => InstallationState == InstallState.Installed;

    public InstallState InstallationState
    {
        get; set;
    }

    private bool _instantiable = true;

    public bool Instantiatable
    {
        get => _instantiable;
        set
        {
            if (_instantiable != value)
            {
                _instantiable = value;
                OnPropertyChanged();
            }
        }
    }

    private int _order = 0;
    public int Order
    {
        get => _order;
        set
        {
            if (_order != value)
            {
                _order = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public string AssemblyLoadPath
    {
        get; set;
    }

    public bool Local { get; set; } = false;

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
