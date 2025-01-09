﻿using Droplex;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Win32;
using NuGet;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything;

public static class EverythingDownloadHelper
{
    public static async Task<string> PromptDownloadIfNotInstallAsync(string installedLocation, IPublicAPI api)
    {
        if (!string.IsNullOrEmpty(installedLocation) && installedLocation.FileExists())
            return installedLocation;

        installedLocation = GetInstalledPath();
              
        if (!File.Exists(installedLocation))
        {
            if (api.ShowMsgBox(
                string.Format(api.GetTranslation("flowlauncher_plugin_everything_installing_select"), Environment.NewLine),
                api.GetTranslation("flowlauncher_plugin_everything_installing_title"),
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var dlg = new System.Windows.Forms.OpenFileDialog
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                };

                var result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    installedLocation = dlg.FileName;
                }
                else
                {
                    return "";
                }
            }
        }

        if (File.Exists(installedLocation))
        {
            return installedLocation;
        }

        api.ShowMsg(api.GetTranslation("flowlauncher_plugin_everything_installing_title"),
            api.GetTranslation("flowlauncher_plugin_everything_installing_subtitle"), "", useMainWindowAsOwner: false);

        await DroplexPackage.Drop(App.Everything).ConfigureAwait(false);

        api.ShowMsg(api.GetTranslation("flowlauncher_plugin_everything_installing_title"),
            api.GetTranslation("flowlauncher_plugin_everything_installationsuccess_subtitle"), "", useMainWindowAsOwner: false);

        //获取安装的路径
        installedLocation = GetInstalledPath();

        //为获取到，则设置默认路径
        if (installedLocation.IsEmpty())
        {
            var location = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            installedLocation = Path.Join(location, "Everything", "Everything.exe");
        }

        if (!installedLocation.FileExists())
        {
            return "";
        }

        Process.Start(installedLocation, "-install-service");

        return installedLocation;
    }

    internal static string GetInstalledPath()
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

        return string.Empty;
    }
}
