using System;
using System.IO;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public static class DataLocation
    {
        public const string PortableFolderName = "UserData";
        public const string DeletionIndicatorFile = ".dead";
        public static string PortableDataPath = Path.Combine(Constant.ProgramDirectory, PortableFolderName);
        public static string RoamingDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");
        public static string DataDirectory()
        {
            if (PortableDataLocationInUse())
            {
                return PortableDataPath;
            }
                
            return RoamingDataPath;
        }

        public static bool PortableDataLocationInUse()
        {
            try
            {
                if(!Directory.Exists(PortableDataPath))
                {
                    Directory.CreateDirectory(PortableDataPath);
                }
                
                if(Path.Combine(PortableDataPath, DeletionIndicatorFile).FileExists())
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string PluginsDirectory {
            get => Path.Combine(DataDirectory(), Constant.Plugins);
        }

        public static string PluginSettingsDirectory
        {
            get => Path.Combine(DataDirectory(), "Settings", Constant.Plugins);
        }

        public const string PythonEnvironmentName = "Python";
        public const string NodeEnvironmentName = "Node.js";
        public const string PluginEnvironments = "FlowEnvironments";        
        public static readonly string PluginEnvironmentsPath = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PluginEnvironments);
    }
}
