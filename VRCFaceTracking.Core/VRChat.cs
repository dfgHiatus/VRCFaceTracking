using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VRCFaceTracking.Core;

public static class VRChat
{
    private static string VRCData
    {
        get
        {
#if WINDOWS_DEBUG || WINDOWS_RELEASE
            // On Windows, VRChat's OSC folder is under %appdata%/LocalLow/VRChat/VRChat
            return Path.Combine(
                $"{Environment.GetEnvironmentVariable("localappdata")}Low",
                "VRChat", "VRChat"
            );
#else
            /* On Linux, things are a little different. The above points to a non-existent folder
             * Thankfully, we can make some assumptions based on the fact VRChat on Linux runs through Proton
             * For reference, here is what a target path looks like:
             * /home/USER_NAME/.steam/steam/steamapps/compatdata/438100/pfx/drive_c/users/steamuser/AppData/LocalLow/VRChat/VRChat/OSC/
             * Where 438100 is VRChat's Steam GameID, and the path after "steam" is pretty much fixed */

            // 1) First, get the user profile folder
            // (/home/USER_NAME/)
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 2) Then, search for common Steam install paths
            // (/home/USER_NAME/.steam/steam/)
            string[] possiblePaths =
            {
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam")
            };
            string steamPath = Array.Find(possiblePaths, Directory.Exists) ?? string.Empty;

            // 3) Finally, append the fixed path to find the OSC folder.
            return string.IsNullOrEmpty(steamPath) ?
                throw new DirectoryNotFoundException("Could not detect Steam install!") :
                Path.Combine(steamPath, "steamapps", "compatdata", "438100", "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat");
#endif
        }
    }

    public static readonly string VRCOSCDirectory = Path.Combine(VRCData, "OSC");

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
