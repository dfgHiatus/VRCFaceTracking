using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Params.Data;

namespace VRCFaceTracking;

public class MainIntegrated
{
    private static readonly CancellationTokenSource MasterCancellationTokenSource = new();
    private readonly ILogger _logger;
    private readonly ILibManager _libManager;
    private readonly UnifiedTrackingMutator _mutator;

    public MainIntegrated(ILoggerFactory loggerFactory, ILibManager libManager, UnifiedTrackingMutator mutator)
    {
        _logger = loggerFactory.CreateLogger("MainIntegrated");
        _libManager = libManager;
        _mutator = mutator;
    }

    public async void Teardown()
    {
        _logger.LogInformation("VRCFT Integrated Exiting!");
        _libManager.TeardownAllAndResetAsync();

        await _mutator.SaveCalibration();

        // Kill our threads
        _logger.LogDebug("Cancelling token sources...");
        MasterCancellationTokenSource.Cancel();
        
        _logger.LogDebug("Resetting our time end period...");
        // Core.Utils.TimeEndPeriod(1);
        
        _logger.LogDebug("Teardown successful. Awaiting exit...");
    }

    public async Task InitializeAsync()
    {
        _libManager.Initialize();
        _mutator.LoadCalibration();

        // Begin main update loop
        _logger.LogDebug("Starting update loop...");
        // Core.Utils.TimeBeginPeriod(1);
        ThreadPool.QueueUserWorkItem(async ct =>
        {
            var token = (CancellationToken)ct;
            
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(10);
                await UnifiedTracking.UpdateData(token);
            }
        }, MasterCancellationTokenSource.Token);

        await Task.CompletedTask;
    }
}