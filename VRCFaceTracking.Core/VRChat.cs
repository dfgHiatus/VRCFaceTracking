﻿using System.Diagnostics;
using System.Runtime.Versioning;
using Gameloop.Vdf;
using Microsoft.Win32;

namespace VRCFaceTracking.Core;

public static class VRChat
{
    static VRChat()
    {
#if WINDOWS_DEBUG || WINDOWS_RELEASE
        VRCOSCDirectory = Path.Combine(
                $"{Environment.GetEnvironmentVariable("localappdata")}Low", "VRChat", "VRChat", "OSC"
        );
#else
        /* On macOS/Linux, things are a little different. The above points to a non-existent folder
         * Thankfully, we can make some assumptions based on the fact VRChat on Linux runs through Proton
         * For reference, here is what a target path looks like:
         * /home/USER_NAME/.steam/steam/steamapps/compatdata/438100/pfx/drive_c/users/steamuser/AppData/LocalLow/VRChat/VRChat/OSC/
         * Where 438100 is VRChat's Steam GameID, and the path after "steam" is pretty much fixed */

        // 1) Get where steam is installed
        using var process = new Process();
        process.StartInfo.FileName = "which";
        process.StartInfo.Arguments = "steam";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        var steamPath = process.StandardOutput.ReadLine();
        process.WaitForExit();

        // 2) Inside the steam install directory, find the file steamPath/steamapps/libraryfolders.vdf
        // This is a special file that tells us where on a users computer their steam libraries are
        var steamLibrariesPaths = Path.Combine(steamPath!, "steamapps", "libraryfolders.vdf");
        dynamic volvo = VdfConvert.Deserialize(File.ReadAllText(steamLibrariesPaths));

        string vrchatPath = null!;
        foreach (dynamic library in volvo.Value)
        {
            if (library.Value["path"] != null && library.Value["apps"] != null)
            {
                string libraryPath = library.Value["path"].ToString();
                dynamic apps = library.Value["apps"];

                // From this, determine which of all the libraries has the VRChat install via its AppID (438100)
                if (apps != null && apps.ContainsKey(438100))
                {
                    vrchatPath = libraryPath;
                    break;
                }
            }
        }

        // 3) Finally, construct the path to the user's VRChat install
        VRCOSCDirectory = Path.Combine(vrchatPath, "steamapps", "compatdata", "438100", "pfx", "drive_c",
            "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
#endif
    }

    public static string VRCOSCDirectory { get; }

    [SupportedOSPlatform("windows")]
    public static bool ForceEnableOsc()
    {
        // Set all registry keys containing osc in the name to 1 in Computer\HKEY_CURRENT_USER\Software\VRChat\VRChat
        var regKey = Registry.CurrentUser.OpenSubKey("Software\\VRChat\\VRChat", true);
        if (regKey == null)
            return true;    // Assume we already have osc enabled

        var keys = regKey.GetValueNames().Where(x => x.StartsWith("VRC_INPUT_OSC") || x.StartsWith("UI.Settings.Osc"));

        var wasOscForced = false;
        foreach (var key in keys)
        {
            if ((int) regKey.GetValue(key) == 0)
            {
                // Osc is likely not enabled
                regKey.SetValue(key, 1);
                wasOscForced = true;
            }
        }

        return wasOscForced;
    }

    public static bool IsVrChatRunning() => Process.GetProcesses().Any(x => x.ProcessName == "VRChat");
}
