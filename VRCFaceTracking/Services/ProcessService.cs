using System.ComponentModel;
using System.Management;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Valve.VR;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Services;

// https://stackoverflow.com/questions/1986249/net-process-monitor#1986856
public class ProcessService
{
    private readonly ILogger _logger;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ManagementEventWatcher manager;

    private bool _autoClose;
    public bool AutoClose
    {
        get => _autoClose;
        set => SetField(ref _autoClose, value);
    }

    public ProcessService(ILoggerFactory logger, ILocalSettingsService localSettingsService, IDispatcherService dispatcherService)
    {
        _logger = logger.CreateLogger("ProcessService");
        _dispatcherService = dispatcherService;
        _localSettingsService = localSettingsService;

        if (!Utils.HasAdmin || manager != null)
        {
            _logger.LogWarning("ProcessService: No admin, skipping...");
            AutoClose = false;
            return;
        }

        _logger.LogDebug("ProcessService: Initializing...");
        Task.Run(LoadConfig).Wait();
        manager = new ManagementEventWatcher(
          new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
        manager.EventArrived += new EventArrivedEventHandler(managerStop_EventArrived);
        manager.Start();
    }

    private void managerStop_EventArrived(object sender, EventArrivedEventArgs e)
    {
        var procName = e.NewEvent.Properties["ProcessName"].Value as string;
        if (string.IsNullOrEmpty(procName)) 
            return;

        if (AutoClose && (procName.StartsWith("vrmonitor") || procName.StartsWith("vrserver")))
        {
            _logger.LogDebug("ProcessService: AutoClose enabled, closing {procName}...", procName);
            manager?.Stop();
            Task.Run(SaveConfig).Wait();
            MainStandalone.MasterCancellationTokenSource.Cancel();
        }
    }

    public async Task LoadConfig()
    {
        _logger.LogDebug("Reading configuration...");
        AutoClose = await _localSettingsService.ReadSettingAsync<bool>("AutoClose");
        _logger.LogDebug("Configuration loaded.");
    }

    public async Task SaveConfig()
    {
        _logger.LogDebug("Saving configuration...");
        await _localSettingsService.SaveSettingAsync("AutoClose", AutoClose);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        _dispatcherService.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
