﻿using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.OSC.DataTypes;

namespace VRCFaceTracking.ViewModels;

public partial class RiskySettingsViewModel : ObservableObject
{
    private readonly IMainService _mainService;
    private readonly ParamSupervisor _paramSupervisor;
    private readonly ILogger<RiskySettingsViewModel> _logger;

    [ObservableProperty] private bool _enabled;

    public bool AllRelevantDebug
    {
        get => _paramSupervisor.AllParametersRelevant;
        set => _paramSupervisor.AllParametersRelevant = value;
    }

    public RiskySettingsViewModel(
        IMainService mainService, 
        ParamSupervisor paramSupervisor, 
        ILogger<RiskySettingsViewModel> logger
        )
    {
        _mainService = mainService;
        _paramSupervisor = paramSupervisor;
        _logger = logger;
    }

    /// <summary>
    /// If something goes very wrong some time during init, this can be used to force retry.
    /// Most modules likely will not like this.
    /// </summary>
    public void ForceReInit()
    {
        _logger.LogInformation("Reinitializing VRCFT...");
        
        _mainService.Teardown();
        
        _mainService.InitializeAsync();
    }

    /// <summary>
    /// Worse case, will delete all persistent data stored by VRCFT. The same data is erased upon uninstall too.
    /// </summary>
    public void ResetVRCFT()
    {
        _logger.LogInformation("Resetting VRCFT...");
        
        // Create a file in the VRCFT folder called "reset"
        // This will cause the app to reset on the next launch
        File.Create(Path.Combine(Utils.PersistentDataDirectory, "reset"));
    }

    /// <summary>
    /// A quick and convenient way of force-deleting all OSC config manifests generated by VRChat in case they get stuck.
    /// We really shouldn't have to do this, but hopefully OSCQuery makes this obsolete eventually
    /// </summary>
    public void ResetAvatarOscManifests()
    {
        _logger.LogInformation("Resetting VRChat avatar configuration...");
        try
        {
            foreach (var userFolder in Directory.GetDirectories(VRChat.VRCOSCDirectory))
            {
                if (Directory.Exists(userFolder + "\\Avatars"))
                {
                    Directory.Delete(userFolder + "\\Avatars", true);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to reset VRChat avatar configuration! {Message}", e.Message);
        }
    }
}