using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

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
    public InstallState InstallationState
    {
        get; set;
    }

    [JsonIgnore]
    public string AssemblyLoadPath
    {
        get; set;
    }

    public bool Local
    {
        get; set;
    }

    public bool IsInstalled
    {
        get; set;
    }

    public bool Instantiatable
    {
        get; set;
    }

    public int Order
    {
        get; set;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
