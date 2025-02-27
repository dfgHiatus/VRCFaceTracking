using System.IO.Compression;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Helpers;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Services;

public class ModuleInstaller
{
    private readonly ILogger<ModuleInstaller> _logger;

    public ModuleInstaller(ILogger<ModuleInstaller> logger)
    {
        _logger = logger;

        if (!Directory.Exists(Utils.CustomLibsDirectory))
        {
            Directory.CreateDirectory(Utils.CustomLibsDirectory);
        }
    }

    // Move a directory using just Copy and Remove as MoveDirectory is not usable across drives
    private static void MoveDirectory(string source, string dest)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dest))
        {
            return;
        }

        if (!Directory.Exists(dest))
        {
            Directory.CreateDirectory(dest);
        }

        // Get files recursively and preserve directory structure
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var path = Path.GetDirectoryName(file);
            var newPath = path?.Replace(source, dest);
            if (newPath == null)
            {
                continue;
            }

            Directory.CreateDirectory(newPath);

            var fileName = Path.GetFileName(file);
            var destFilePath = Path.Combine(newPath, fileName);

            // Skip copying module.json if it already exists in the destination
            if (fileName.Equals("module.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destFilePath))
            {
                continue;
            }

            // Copy the file (overwrite if it exists)
            File.Copy(file, destFilePath, true);
        }

        // Now we delete the source directory
        Directory.Delete(source, true);
    }

    private static async Task DownloadModuleToFile(TrackingModuleMetadata moduleMetadata, string filePath)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(moduleMetadata.DownloadUrl);
        var content = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(filePath, content);
        await Task.CompletedTask;
    }

    /* Removes the 'downloaded from the internet' attribute from a module
     * @param DLL file path
     * @return error; if true then the module should be skipped
     */
    private bool RemoveZoneIdentifier(string path)
    {
        string zoneFile = path + ":Zone.Identifier";

        if (Utils.GetFileAttributes(zoneFile) == 0xffffffff) // INVALID_FILE_ATTRIBUTES
            //zone file doesn't exist, everything's good
            return false;

        if (Utils.DeleteFile(zoneFile))
            _logger.LogDebug("Removing the downloaded file identifier from " + path);
        else
        {
            _logger.LogError("Couldn't removed the 'file downloaded' mark from the " + path + " module! Please unblock the file manually");
            return true;
        }

        return false;
    }

    private string TryFindModuleDll(string moduleDirectory, TrackingModuleMetadata moduleMetadata)
    {
        // Attempt to find the first DLL. If there's more than one, try find the one with the same name as the module
        var dllFiles = Directory.GetFiles(moduleDirectory, "*.dll");

        switch (dllFiles.Length)
        {
            case 0:
                return null;
            // If there's only one, just return it
            case 1:
                return Path.GetFileName(dllFiles[0]);
        }

        // Else we'll try find the one with the closest name to the module using Levenshtein distance
        var targetFileName = Path.GetFileNameWithoutExtension(moduleMetadata.DownloadUrl);
        var dllFile = dllFiles.Select(x => new { FileName = Path.GetFileNameWithoutExtension(x), Distance = LevenshteinDistance.Calculate(targetFileName, Path.GetFileNameWithoutExtension(x)) }).MinBy(x => x.Distance);

        if (dllFile == null)
        {
            _logger.LogError(
                "Module {module} has no .dll file name specified and no .dll files were found in the extracted zip",
                moduleMetadata.ModuleId);
            return null;
        }

        _logger.LogDebug("Module {module} didn't specify a target dll, and contained multiple. Using {dll} as its distance of {distance} was closest to the module name",
            moduleMetadata.ModuleId, dllFile.FileName, dllFile.Distance);
        return Path.GetFileName(dllFile.FileName);
    }

    public async Task<string> InstallLocalModule(string zipPath)
    {
        // First, we copy the zip to our custom libs directory
        var fileName = Path.GetFileName(zipPath);
        var newZipPath = Path.Combine(Utils.CustomLibsDirectory, fileName);
        File.Copy(zipPath, newZipPath, true);

        // Second, we unzip it
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(zipPath));
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, true);
        }
        Directory.CreateDirectory(tempDirectory);
        ZipFile.ExtractToDirectory(newZipPath, tempDirectory);
        File.Delete(newZipPath);

        // Now, we need to find the module.json file and deserialize it
        var moduleJsonPath = Path.Combine(tempDirectory, "module.json");
        if (!File.Exists(moduleJsonPath))
        {
            _logger.LogError("Module {module} does not contain a module.json file", fileName);
            Directory.Delete(tempDirectory, true);
            return null;
        }

        var moduleMetadata = await Json.ToObjectAsync<TrackingModuleMetadata>(await File.ReadAllTextAsync(moduleJsonPath));
        if (moduleMetadata == null)
        {
            _logger.LogError("Module {module} contains an invalid module.json file", fileName);
            Directory.Delete(tempDirectory, true);
            return null;
        }

        // Now we move to a directory named after the module id and delete the temp directory
        var moduleDirectory = Path.Combine(Utils.CustomLibsDirectory, moduleMetadata.ModuleId.ToString());
        if (Directory.Exists(moduleDirectory))
        {
            Directory.Delete(moduleDirectory, true);
        }

        MoveDirectory(tempDirectory, moduleDirectory);

        // Now we need to find the module's dll
        moduleMetadata.DllFileName ??= TryFindModuleDll(moduleDirectory, moduleMetadata);
        if (moduleMetadata.DllFileName == null)
        {
            _logger.LogError("Module {module} has no .dll file name specified and no .dll files were found in the extracted zip", moduleMetadata.ModuleId);
            return null;
        }

        // Now we write the module.json file to the module directory
        await File.WriteAllTextAsync(Path.Combine(moduleDirectory, "module.json"), JsonConvert.SerializeObject(moduleMetadata, Formatting.Indented));

        // Finally, we return the module's dll file name
        return Path.Combine(moduleDirectory, moduleMetadata.DllFileName);
    }

    public async Task<string> InstallRemoteModule(TrackingModuleMetadata moduleMetadata)
    {
        var moduleDirectory = Path.Combine(Utils.CustomLibsDirectory, moduleMetadata.ModuleId.ToString());

        try
        {
            // Ensure the module directory exists
            if (!Directory.Exists(moduleDirectory))
            {
                Directory.CreateDirectory(moduleDirectory);
            }

            // Handle .dll and non-.dll downloads differently
            var downloadExtension = Path.GetExtension(moduleMetadata.DownloadUrl);
            if (downloadExtension != ".dll")
            {
                await HandleNonDllDownload(moduleMetadata, moduleDirectory);
            }
            else
            {
                await HandleDllDownload(moduleMetadata, moduleDirectory);
            }

            // Merge and save metadata
            await MergeAndSaveMetadata(moduleMetadata, moduleDirectory);

            _logger.LogInformation("Installed module {module} to {moduleDirectory}", moduleMetadata.ModuleId, moduleDirectory);
            return Path.Combine(moduleDirectory, moduleMetadata.DllFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to install module {moduleMetadata.ModuleName} (ID: {moduleMetadata.ModuleId})");
            return null;
        }
    }

    private async Task HandleNonDllDownload(TrackingModuleMetadata moduleMetadata, string moduleDirectory)
    {
        _logger.LogDebug($"Provisioning temp download dir for {moduleMetadata.ModuleName}");
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            // Create and clean up the temp directory
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
            Directory.CreateDirectory(tempDirectory);

            // Download the zip file
            string tempZipPath = Path.Combine(tempDirectory, "module.zip");
            _logger.LogInformation($"Downloading {moduleMetadata.ModuleName} to temp dir {tempDirectory}");
            await DownloadModuleToFile(moduleMetadata, tempZipPath);

            // Extract the zip file
            _logger.LogInformation($"Extracting zip to {tempDirectory}");
            ZipFile.ExtractToDirectory(tempZipPath, tempDirectory);
            File.Delete(tempZipPath);

            // Move extracted files to the module directory
            _logger.LogInformation($"Moving extracted files for {moduleMetadata.ModuleName} to {moduleDirectory}");
            MoveDirectory(tempDirectory, moduleDirectory);

            // Unblock DLLs on Windows
            if (OperatingSystem.IsWindows())
            {
                foreach (var dll in Directory.GetFiles(moduleDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    RemoveZoneIdentifier(dll);
                }
            }

            // Ensure a valid .dll file name is set
            moduleMetadata.DllFileName ??= TryFindModuleDll(moduleDirectory, moduleMetadata);
            if (moduleMetadata.DllFileName == null)
            {
                throw new InvalidOperationException($"Module {moduleMetadata.ModuleId} has no .dll file name specified and no .dll files were found in the extracted zip.");
            }
        }
        finally
        {
            // Clean up the temp directory
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    private async Task HandleDllDownload(TrackingModuleMetadata moduleMetadata, string moduleDirectory)
    {
        // Set the .dll file name if not provided
        moduleMetadata.DllFileName ??= Path.GetFileName(moduleMetadata.DownloadUrl);

        var dllPath = Path.Combine(moduleDirectory, moduleMetadata.DllFileName);

        // Ensure the module directory exists
        if (!Directory.Exists(moduleDirectory))
        {
            Directory.CreateDirectory(moduleDirectory);
        }

        // Download the .dll file
        await DownloadModuleToFile(moduleMetadata, dllPath);
        _logger.LogDebug("Downloaded module {module} to {dllPath}", moduleMetadata.ModuleId, dllPath);
    }

    private async Task MergeAndSaveMetadata(TrackingModuleMetadata moduleMetadata, string moduleDirectory)
    {
        var moduleJsonPath = Path.Combine(moduleDirectory, "module.json");

        // Load existing metadata if it exists
        InstallableTrackingModule existingMetadata = null;
        if (File.Exists(moduleJsonPath))
        {
            var existingJson = await File.ReadAllTextAsync(moduleJsonPath);
            existingMetadata = JsonConvert.DeserializeObject<InstallableTrackingModule>(existingJson);
        }

        // Merge metadata (preserve user-modified fields if any)
        if (existingMetadata != null)
        {
            UpdateMetadataProperties(existingMetadata, moduleMetadata);
            moduleMetadata = existingMetadata;
        }

        // Save the updated metadata
        await File.WriteAllTextAsync(moduleJsonPath, JsonConvert.SerializeObject(moduleMetadata, Formatting.Indented));
    }

    private void UpdateMetadataProperties(InstallableTrackingModule target, TrackingModuleMetadata source)
    {
        var properties = typeof(TrackingModuleMetadata).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanWrite || property.SetMethod == null)
                continue;

            var sourceValue = property.GetValue(source);

            if (sourceValue != null)
            {
                property.SetValue(target, sourceValue);
            }
        }
    }

    public void MarkModuleForDeletion(InstallableTrackingModule module)
    {
        module.InstallationState = InstallState.AwaitingRestart;
        module.Instantiatable = false;
        var moduleJsonPath = Path.Combine(Utils.CustomLibsDirectory, module.ModuleId.ToString(), "module.json");
        try
        {
            File.WriteAllText(moduleJsonPath, JsonConvert.SerializeObject(module, Formatting.Indented));
            _logger.LogInformation("Marked module {module} for deletion", module.ModuleId);
        }
        catch
        {
            _logger.LogWarning("Attempted to mark module {module} for deletion, but it didn't exist", module.ModuleId);
        }
    }

    public void UninstallModule(TrackingModuleMetadata moduleMetadata)
    {
        _logger.LogDebug("Uninstalling module {module}", moduleMetadata.ModuleId);
        var moduleDirectory = Path.Combine(Utils.CustomLibsDirectory, moduleMetadata.ModuleId.ToString());
        if (Directory.Exists(moduleDirectory))
        {
            try
            {
                Directory.Delete(moduleDirectory, true);
                _logger.LogInformation("Uninstalled module {module} from {moduleDirectory}", moduleMetadata.ModuleId, moduleDirectory);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to uninstall module {module} from {moduleDirectory}", moduleMetadata.ModuleId, moduleDirectory);
            }
        }
        else
        {
            _logger.LogDebug("Module {module} could not be found where it was expected in {moduleDirectory}", moduleMetadata.ModuleId, moduleDirectory);
        }
    }
}
