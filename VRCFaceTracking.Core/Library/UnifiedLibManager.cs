using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.BabbleNative;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Core.Library;

public class UnifiedLibManager : ILibManager
{
    #region Logger
    private readonly ILogger<UnifiedLibManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    #endregion

    #region Observables
    public ObservableCollection<ModuleMetadata> LoadedModulesMetadata { get; set; }
    private readonly IDispatcherService _dispatcherService;
    #endregion

    #region Statuses
    public static ModuleState EyeStatus { get; private set; }
    public static ModuleState ExpressionStatus { get; private set; }
    #endregion

    #region Modules
    private List<Assembly> AvailableModules { get; set; }
    private readonly List<ModuleRuntimeInfo> _moduleThreads = new();
    private readonly IModuleDataService _moduleDataService;
    #endregion

    #region Thread
    private Thread _initializeWorker;
    #endregion

    public UnifiedLibManager(ILoggerFactory factory, IDispatcherService dispatcherService, IModuleDataService moduleDataService)
    {
        _loggerFactory = factory;
        _logger = factory.CreateLogger<UnifiedLibManager>();
        _dispatcherService = dispatcherService;
        _moduleDataService = moduleDataService;

        LoadedModulesMetadata = new ObservableCollection<ModuleMetadata>();
    }

    public void Initialize()
    {
        LoadedModulesMetadata.Clear();
        LoadedModulesMetadata.Add(new ModuleMetadata
        { 
            Active = false,
            Name = "Initializing Modules..."
        });

        // Start Initialization
        _initializeWorker = new Thread(() =>
        {
            // Kill lingering threads
            TeardownAllAndResetAsync();

            // DON'T Find all modules. Just use the hardcoded one to get around Android sandboxing
            // var modules = _moduleDataService.GetInstalledModules().Concat(_moduleDataService.GetLegacyModules());
            // var modulePaths = modules.Select(m => m.AssemblyLoadPath);

            // DON'T Load all modules
            // List<ASL> asls = LoadAssembliesFromPath(modulePaths.ToArray());
            // AvailableModules = new List<Assembly>();
            // foreach (ASL asl in asls)
            //     AvailableModules.Add(asl.Assembly!);

            // Attempt to initialize the requested runtimes.
            //if (AvailableModules != null)
            //{
            //    _logger.LogDebug("Initializing requested runtimes...");
            //    InitRequestedRuntimes(asls);
            //}
            //else
            //{
            //    _dispatcherService.Run(() =>
            //    {
            //        LoadedModulesMetadata.Clear();
            //        LoadedModulesMetadata.Add(new ModuleMetadata
            //        {
            //            Active = false,
            //            Name = "No Modules Loaded"
            //        });
            //    });
            //    _logger.LogWarning("No modules loaded.");
            //}

            _logger.LogDebug("Initializing requested runtimes...");
            InitRequestedRuntimes();
        });
        _logger.LogInformation("Starting initialization tracking");
        _initializeWorker.Start();
    }

    private ExtTrackingModule LoadExternalModule(ASL asl)
    {
        _logger.LogInformation("Loading External Module " + asl);

        try
        {
            // Get the first class that implements ExtTrackingModule
            var module = asl.Assembly.GetTypes().FirstOrDefault(t => t.IsSubclassOf(typeof(ExtTrackingModule)));
            if (module == null)
            {
                throw new Exception("Failed to get module's ExtTrackingModule impl");
            }
            var moduleObj = (ExtTrackingModule)Activator.CreateInstance(module);

            return moduleObj;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception loading {dll}. Skipping. {e}", asl.Assembly.FullName, e);
        }

        return null;
    }

    private List<ASL> LoadAssembliesFromPath(string[] path)
    {
        var returnList = new List<ASL>();
        foreach (var dll in path)
        {
            try
            {
                //var alc = new AssemblyLoadContext(dll, true);
#if NET7_0_OR_GREATER
                var asl = new ASL(dll, true);
#elif NETSTANDARD || NETFRAMEWORK
                var asl = new ASL();
#endif
                var loaded = asl.LoadFile(dll);
                
                var references = loaded.GetReferencedAssemblies();
                var oldRefs = false;
                foreach (var reference in references)
                {
                    if (reference.Name == "VRCFaceTracking" || reference.Name == "VRCFaceTracking.Core")
                    {
                        if (reference.Version < new Version(5, 0, 0, 0))
                        {
                            _logger.LogWarning("Module {dll} references an older version of VRCFaceTracking. Skipping.", Path.GetFileName(dll));
                            oldRefs = true;
                        }
                    }
                }
                if (oldRefs)
                {
                    continue;
                }

                foreach(var type in loaded.GetExportedTypes())
                {
                    if (type.BaseType == typeof(ExtTrackingModule))
                    {
                        _logger.LogDebug("{module} properly implements ExtTrackingModule.", type.Name);
                        returnList.Add(asl);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(" Assembly not able to be loaded. Exception: " + e);
            }
        }

        return returnList;
    }

    private void EnsureModuleThreadStarted(ExtTrackingModule module)
    {
        if (_moduleThreads.Any(pair => pair.Module == module))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var thread = new Thread(() =>
        {
            _logger.LogDebug("Starting thread for {module}", module.GetType().Name);
            while (!cts.IsCancellationRequested)
            {
                module.Update();
            }
            _logger.LogDebug("Thread for {module} ended", module.GetType().Name);
        });
        thread.Start();

        var runtimeModules = new ModuleRuntimeInfo
        {
            Module = module,
            UpdateCancellationToken = cts,
            asl = null,
            UpdateThread = thread
        };

        _moduleThreads.Add(runtimeModules);
    }

    private void AttemptModuleInitialize(ExtTrackingModule module)
    {
        if (module.Supported is { SupportsEye: false, SupportsExpression: false })
        {
            return;
        }

        module.Logger = _loggerFactory.CreateLogger(module.GetType().Name);

        bool eyeSuccess, expressionSuccess;
        try
        {
            (eyeSuccess, expressionSuccess) = module.Initialize(EyeStatus == ModuleState.Uninitialized, ExpressionStatus == ModuleState.Uninitialized);
        }
        catch (MissingMethodException)
        {
            _logger.LogError("{moduleName} does not properly implement ExtTrackingModule. Skipping.", module.GetType().Name);
            return;
        }
        catch (Exception e)
        {
            _logger.LogError("Exception initializing {module}. Skipping. {e}", module.GetType().Name, e);
            return;
        }

        module.ModuleInformation.OnActiveChange = state =>
            module.Status = state ? ModuleState.Active : ModuleState.Idle;

        // Skip any modules that don't succeed, otherwise set UnifiedLib to have these states active and add module to module list.
        if (!eyeSuccess && !expressionSuccess)
        {
            return;
        }

        EyeStatus = eyeSuccess ? ModuleState.Active : ModuleState.Uninitialized;
        ExpressionStatus = expressionSuccess ? ModuleState.Active : ModuleState.Uninitialized;
        
        module.ModuleInformation.Active = true;
        module.ModuleInformation.UsingEye = eyeSuccess;
        module.ModuleInformation.UsingExpression = expressionSuccess;
        _dispatcherService.Run(() => { 
            if (!LoadedModulesMetadata.Contains(module.ModuleInformation))
            {
                LoadedModulesMetadata.Add(module.ModuleInformation);
            }
        });
        EnsureModuleThreadStarted(module);
    }

    private void InitRequestedRuntimes()
    {
        _logger.LogInformation("Initializing runtimes...");

        _logger.LogInformation("Initializing hard-coded Babble Module...");
        var loadedModule = new BabbleModule();
        AttemptModuleInitialize(loadedModule);

        if (_moduleThreads.Count == 0)
        {
            _logger.LogWarning("No modules loaded.");
            _dispatcherService.Run(() =>
            {
                LoadedModulesMetadata.Clear();
                LoadedModulesMetadata.Add(new ModuleMetadata
                {
                    Active = false,
                    Name = "No Modules Loaded"
                });
            });
        }
        else
        {
            _dispatcherService.Run(() =>
            {
                // Remove our dummy module
                LoadedModulesMetadata.RemoveAt(0);
            });
            foreach (var pair in _moduleThreads)
            {
                if (pair.Module.ModuleInformation.Active)
                {
                    _logger.LogInformation("Tracking initialized via {module}", pair.Module.ToString());
                }
            }
        }
    }

    private bool TeardownModule(ModuleRuntimeInfo module)
    {
        _logger.LogInformation("Tearing down {module} ", module.Module.GetType().Name);
        module.UpdateCancellationToken.Cancel();
        if (module.UpdateThread.IsAlive)
        {
            // Edge case, we wait for the thread to finish before unloading the assembly
            _logger.LogDebug("Waiting for {module}'s thread to join...", module.Module.GetType().Name);
            module.UpdateThread.Join();
        }

        module.Module.Teardown();
        // We set this to null earlier, nothing to do
        // module.asl.Unload();
        
        return true;
    }

    // Signal all active modules to gracefully shut down their respective runtimes
    public void TeardownAllAndResetAsync()
    {
        _logger.LogInformation("Tearing down all modules...");

        foreach (var module in _moduleThreads)
        {
            var success = false;
            try
            {
                success = TeardownModule(module);
            }
            finally
            {
                if (!success)
                {
                    _logger.LogWarning($"Module: {module.Module.ModuleInformation.Name} failed to shut down. Killing its thread.");
                    module.UpdateThread.Interrupt();
                }
            }
        }

        _moduleThreads.Clear();
        
        EyeStatus = ModuleState.Uninitialized;
        ExpressionStatus = ModuleState.Uninitialized;
    }
}