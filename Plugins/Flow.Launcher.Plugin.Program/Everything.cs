using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using System.IO;

namespace Flow.Launcher.Plugin.Program;

public static class Everything
{
    public static string GetInstalledPath()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (key is not null)
        {
            foreach (var subKey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
            {
                if (subKey?.GetValue("DisplayName") is not string displayName || !displayName.Contains("Everything"))
                {
                    continue;
                }
                if (subKey.GetValue("UninstallString") is not string uninstallString)
                {
                    continue;
                }

                if (Path.GetDirectoryName(uninstallString) is not { } uninstallDirectory)
                {
                    continue;
                }
                return Path.Combine(uninstallDirectory, "Everything.exe");
            }
        }

        var location = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Join(location, "Everything", "Everything.exe");
    }

    public static IEnumerable<string> EverytingFindFiles(string[] dirs, string[] suffixes, string query)
    {
        EverythingApi.Everything_GetMajorVersion();
        if (EverythingApi.Everything_GetLastError() != 0)
        {
            var installedLocation = GetInstalledPath();
            Process.Start(installedLocation, "-startup");

            EverythingApi.Everything_GetMajorVersion();
            if (EverythingApi.Everything_GetLastError() != 0)
            {
                return Enumerable.Empty<string>();
            }
        }

        var ds = dirs.Select((x) => $"\"{x}\\\"");
        var ss = suffixes.Select((x) => $"ext:\"{x}\"");

        var strDir = string.Join("|", ds);
        var strSuff = string.Join("|", ss);

        var search = $"{strSuff} \"{query}\" {strDir}";

        var ret = new List<string>();

        EverythingApi.Everything_SetSearchW(search);
        EverythingApi.Everything_QueryW(true);

        int numResults = EverythingApi.Everything_GetNumResults();
        var result = new System.Text.StringBuilder(512);

        for (int i = 0; i < numResults; i++)
        {
            EverythingApi.Everything_GetResultFullPathNameW(i, result, result.Capacity);
            ret.Add(result.ToString());
        }

        return ret;
    }

    public static Result NewResult(string path, bool hideAppsPath)
    {
        var result = new Result
        {
            Title = Path.GetFileNameWithoutExtension(path),
            SubTitle = !hideAppsPath ? path : "",
            IcoPath = path,
            TitleToolTip = $"{Path.GetFileNameWithoutExtension(path)}\n{path}",
            Action = c =>
            {
                // Ctrl + Enter to open containing folder
                bool openFolder = c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control;
                if (openFolder)
                {
                    Main.Context.API.OpenDirectory(Path.GetDirectoryName(path), path);
                    return true;
                }

                // Ctrl + Shift + Enter to run as admin
                bool runAsAdmin = c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift);

                var info = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true,
                    Verb = runAsAdmin ? "runas" : "",
                };

                _ = Task.Run(() => Main.StartProcess(Process.Start, info));

                return true;
            }
        };

        return result;
    }
}
