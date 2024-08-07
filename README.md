# 👀 VRCFaceTracking

Provides real eye tracking and lip tracking to be integrated directly in Unity, skipping the need for OSC. **THIS WILL NOT WORK WITH VRCHAT!** This is designed to be used in any Unity projects (or possibly modded in to any other Unity game 👀).

Get started [here!](https://docs.vrcft.io/docs/intro/getting-started)

[![Discord](https://discord.com/api/guilds/849300336128032789/widget.png)](https://discord.gg/Fh4FNehzKn)

## 🎥 Demo

[![](https://i.imgur.com/iQkw12C.jpg)](https://youtu.be/ZTVnh8aaf9U)

## 😢 Setup with Unity

[Download the Latest Release](https://github.com/TigersUniverse/VRCFaceTracking/releases/latest) and import all the assemblies into Unity. Next, simply make all the needed interfaces (ILogger, IDisposable, ILoggerFactory, IDispatcherService, and ILocalSettingsService). Finally, plug this all into a MainIntegrated object and invoke the InitializeAsync() method.

```cs
// Assume settings, loggerFactory, and dispatcher (these will be implemented for your needs)
ILogger<ModuleDataService> moduleDataServiceLogger = loggerFactory.CreateLogger<ModuleDataService>();
ILogger<UnifiedTrackingMutator> mutatorLogger = loggerFactory.CreateLogger<UnifiedTrackingMutator>();
IModuleDataService moduleDataService = new ModuleDataService(moduleDataServiceLogger);
ILibManager libManager = new UnifiedLibManager(loggerFactory, dispatcher, moduleDataService);
UnifiedTrackingMutator mutator = new UnifiedTrackingMutator(mutatorLogger, dispatcher, settings);
MainIntegrated mainIntegrated = new MainIntegrated(loggerFactory, libManager, mutator);
mainIntegrated.InitializeAsync();
// When you're done,
mainIntegrated.Teardown();
```

For a more in-depth example, see [FaceTrackingManager.cs](https://github.com/TigersUniverse/Hypernex.Unity/blob/main/Assets/Scripts/ExtendedTracking/FaceTrackingManager.cs) and [FaceTrackingServices.cs](https://github.com/TigersUniverse/Hypernex.Unity/blob/main/Assets/Scripts/ExtendedTracking/FaceTrackingServices.cs)

## 🛠 Parameter Info

### [List of Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/)

## ⛓ External Modules

Modules for Unity must be rebuilt with this assembly. Be sure to build assemblies with `net7.0`, `netstandard2.1`, and `net481`!

Use the following modules to add support for other hardware, when using VRCFaceTracking in Unity:

* [SRanipalTrackingModule](https://github.com/TigersUniverse/SRanipalTrackingModule) - Adds support for the Vive Pro Eye and the Vive Lip Tracker
* [VRCFTOmniceptModule](https://github.com/TigersUniverse/VRCFTOmniceptModule) - Adds support for HP Omnicept eye tracking
* [VRCFaceTracking-LiveLink](https://github.com/TigersUniverse/VRCFaceTracking-LiveLink) - Adds support for the Live Link app
* [ETVRTrackingModule](https://github.com/TigersUniverse/ETVRTrackingModule) - Adds support for ETVR
* [VRCFT-Babble](https://github.com/TigersUniverse/VRCFT-Babble) - Adds support for Babble
