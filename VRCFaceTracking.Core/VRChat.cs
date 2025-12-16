using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace VRCFaceTracking.Core;

/// <summary>
/// Helper class for VRChat-specific operations
/// </summary>
public static class VRChat
{
    static VRChat()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, things are easy-peasy for us
            VRCOSCDirectory = Path.Combine(
                $"{Environment.GetEnvironmentVariable("localappdata")}Low", "VRChat", "VRChat", "OSC"
            );
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
             // On macOS/Linux, things are a little different. The above points to a non-existent folder
            // Thankfully, we can make some assumptions based on the fact VRChat on Linux runs through Proton

            // 1) First, get the user profile folder
            // (/home/USER_NAME/)
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 2) Then, search for common Steam install paths
            // (/home/USER_NAME/.steam/steam/)
            string[] possibleSteamPaths =
            [
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam")
            ];
            var steamPath = Array.Find(possibleSteamPaths, Directory.Exists);
            var vrChatPath = string.Empty;

            // 3) Inside the steam install directory, find the file steamPath/steamapps/libraryfolders.vdf
            // This is a special file that tells us where on a users computer their steam libraries are
            if (!string.IsNullOrEmpty(steamPath))
            {
                var steamLibrariesPaths = Path.Combine(steamPath!, "steamapps", "libraryfolders.vdf");
                dynamic volvo = VdfConvert.Deserialize(File.ReadAllText(steamLibrariesPaths));

                foreach (var library in volvo.Value)
                {
                    if (library.Value["path"] == null || library.Value["apps"] == null)
                    {
                        continue;
                    }

                    string libraryPath = library.Value["path"].ToString();
                    VObject apps = library.Value["apps"];

                    // From this, determine which of all the libraries has the VRChat install via its AppID (438100)
                    if (apps == null || !apps.ContainsKey("438100"))
                    {
                        continue;
                    }

                    vrChatPath = libraryPath;
                    break;
                }
            }

            /* 4) Edge case! Here, if:
             A) VRChat was NOT detected OR
             B) VRChat was detected, BUT it's NOT running
             An avatar emulator might be trying to use us!
             For reference, here is what an emulator path looks like on MacOS. Gotta have variety:
             /Users/[user]/.local/share/VRChat/vrchat/OSC/
             We need to try this first before defaulting to the game path */
            string[] possibleEmulatorPaths =
            [
                Path.Combine(home, ".local", "share", "VRChat")
            ];
            var emulatorPath = Array.Find(possibleEmulatorPaths, Directory.Exists);
            var isVRChatInactive = string.IsNullOrEmpty(vrChatPath) || !IsVrChatRunning();

            if (!string.IsNullOrEmpty(emulatorPath) && isVRChatInactive)
            {
                // Construct the path to the avatar emulator's data folder
                VRCOSCDirectory = Path.Combine(emulatorPath, "vrchat", "OSC");
            }

            /* 5) Finally, construct the path to the user's VRChat install
            For reference, here is what a target path looks like:
            /home/USER_NAME/.steam/steam/steamapps/compatdata/438100/pfx/drive_c/users/steamuser/AppData/LocalLow/VRChat/VRChat/OSC/
            Where 438100 is VRChat's Steam GameID, and the path after "steam" is pretty much fixed */
            else if (!string.IsNullOrEmpty(vrChatPath))
            {
                VRCOSCDirectory = Path.Combine(vrChatPath, "steamapps", "compatdata", "438100", "pfx",
                    "drive_c", "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
            }
        }
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

    public static bool IsVrChatRunning() => Process.GetProcesses().Any(x => Regex.IsMatch(x.ProcessName, "^VRChat(.exe)?$"));
}
