using Microsoft.Win32;
using Squirrel;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using System.Linq;

namespace Flow.Launcher.Core.Configuration
{
    public class Portable : IPortable
    {
        /// <summary>
        /// As at Squirrel.Windows version 1.5.2, UpdateManager needs to be disposed after finish
        /// </summary>
        /// <returns></returns>
        private UpdateManager NewUpdateManager()
        {
            var applicationFolderName = Constant.ApplicationDirectory
                                            .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None)
                                            .Last();

            return new UpdateManager(string.Empty, applicationFolderName, Constant.RootDirectory);
        }

        public void DisablePortableMode()
        {
            try
            {
                //MoveUserDataFolder(DataLocation.PortableDataPath, DataLocation.RoamingDataPath);
#if !DEBUG
                // Create shortcuts and uninstaller are not required in debug mode, 
                // otherwise will repoint the path of the actual installed production version to the debug version
                CreateShortcuts();
                CreateUninstallerEntry();
#endif
                RemoveDeletion(DataLocation.RoamingDataPath);
                IndicateDeletion(DataLocation.PortableDataPath);

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                Log.Exception("|Portable.DisablePortableMode|Error occurred while disabling portable mode", e);
            }
        }

        public void EnablePortableMode()
        {
            try
            {
                //MoveUserDataFolder(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
#if !DEBUG
                // Remove shortcuts and uninstaller are not required in debug mode, 
                // otherwise will delete the actual installed production version
                RemoveShortcuts();
                RemoveUninstallerEntry();
#endif
                RemoveDeletion(DataLocation.PortableDataPath);
                IndicateDeletion(DataLocation.RoamingDataPath);

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                Log.Exception("|Portable.EnablePortableMode|Error occurred while enabling portable mode", e);
            }
        }

        public void RemoveShortcuts()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu);
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop);
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup);
            }
        }

        public void RemoveUninstallerEntry()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.RemoveUninstallerRegistryEntry();
            }
        }

        public void MoveUserDataFolder(string fromLocation, string toLocation)
        {
            FilesFolders.CopyAll(fromLocation, toLocation, MessageBoxEx.Show);
            VerifyUserDataAfterMove(fromLocation, toLocation);
        }

        public void VerifyUserDataAfterMove(string fromLocation, string toLocation)
        {
            FilesFolders.VerifyBothFolderFilesEqual(fromLocation, toLocation, MessageBoxEx.Show);
        }

        public void CreateShortcuts()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu, false);
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop, false);
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup, false);
            }
        }

        public void CreateUninstallerEntry()
        {
            var uninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var subKey1 = baseKey.CreateSubKey(uninstallRegSubKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
            using (var subKey2 = subKey1.CreateSubKey(Constant.FlowLauncher, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                subKey2.SetValue("DisplayIcon", Path.Combine(Constant.ApplicationDirectory, "app.ico"), RegistryValueKind.String);
            }

            using (var portabilityUpdater = NewUpdateManager())
            {
                _ = portabilityUpdater.CreateUninstallerRegistryEntry();
            }
        }

        internal void IndicateDeletion(string filePathTodelete)
        {
            var deleteFilePath = Path.Combine(filePathTodelete, DataLocation.DeletionIndicatorFile);
            File.CreateText(deleteFilePath);
        }

        internal void RemoveDeletion(string filePathTodelete)
        {
            var deleteFilePath = Path.Combine(filePathTodelete, DataLocation.DeletionIndicatorFile);
            deleteFilePath.Delete();
        }

        ///<summary>
        ///This method should be run at first before all methods during start up and should be run before determining which data location
        ///will be used for Flow Launcher.
        ///</summary>
        public void PreStartCleanUpAfterPortabilityUpdate()
        {
            //便携模式判断
            var portablePath = DataLocation.PortableDataPath;
            var portableLocationExists = portablePath.LocationExists();
            var portableDataDeleteFile = Path.Combine(portablePath, DataLocation.DeletionIndicatorFile);
            if (portableLocationExists && !portableDataDeleteFile.FileExists())
            {
                return;
            }

            //漫游模式判断
            var roamingPath = DataLocation.RoamingDataPath;
            var roamingLocationExists = roamingPath.LocationExists();
            var roamingDataDeleteFile = Path.Combine(roamingPath, DataLocation.DeletionIndicatorFile);
            if (roamingLocationExists && !roamingDataDeleteFile.FileExists())
            {
                return;
            }

            //优先使用便携
            if (!portableLocationExists)
            {
                portablePath.CreateDir();
            }

            portableDataDeleteFile.Delete();
        }

        public bool CanUpdatePortability()
        {
            return true;
        }
    }
}
