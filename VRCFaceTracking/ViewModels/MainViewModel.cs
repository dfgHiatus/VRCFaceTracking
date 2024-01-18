﻿using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Models.ParameterDefinition;
using VRCFaceTracking.Core.Params;

namespace VRCFaceTracking.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    public ILibManager LibManager
    {
        get;
    }
    
    public ParameterOutputService ParameterOutputService
    {
        get;
    }

    [ObservableProperty] private IAvatarInfo _currentlyLoadedAvatar;

    [ObservableProperty] private List<Parameter> _currentParameters;

    private int _messagesRecvd;
    [ObservableProperty] private int _messagesInPerSec;

    private int _messagesSent;
    [ObservableProperty] private int _messagesOutPerSec;

    [ObservableProperty] private bool _noModulesInstalled;
    
    [ObservableProperty] private bool _oscWasDisabled;

    public MainViewModel()
    {
        //Services
        LibManager = App.GetService<ILibManager>();
        ParameterOutputService = App.GetService<ParameterOutputService>();
        var moduleDataService = App.GetService<IModuleDataService>();
        var dispatcherService = App.GetService<IDispatcherService>();
        
        // Modules
        var installedNewModules = moduleDataService.GetInstalledModules();
        var installedLegacyModules = moduleDataService.GetLegacyModules().Count();
        NoModulesInstalled = !installedNewModules.Any() && installedLegacyModules == 0;
        
        // Avatar Info
        CurrentlyLoadedAvatar = new NullAvatarDef("Loading...", "Loading...");
        ParameterOutputService.OnAvatarLoaded += (info, list) => dispatcherService.Run(() =>
        {
            CurrentlyLoadedAvatar = info;
            CurrentParameters = list;
        });
        
        // Message Timer
        ParameterOutputService.OnMessageReceived += _ => { _messagesRecvd++; };
        ParameterOutputService.OnMessageDispatched += () => { _messagesSent++; };
        var messageTimer = new DispatcherTimer();
        messageTimer.Interval = TimeSpan.FromSeconds(1);
        messageTimer.Tick += (sender, args) =>
        {
            MessagesInPerSec = _messagesRecvd;
            _messagesRecvd = 0;
            
            MessagesOutPerSec = _messagesSent;
            _messagesSent = 0;
        };
        messageTimer.Start();
    }
}
