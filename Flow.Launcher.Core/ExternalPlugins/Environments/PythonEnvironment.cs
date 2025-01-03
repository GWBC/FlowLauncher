using Droplex;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class PythonEnvironment : AbstractPluginEnvironment
    {
        internal override string Language => AllowedLanguage.Python;

        internal override string EnvName => DataLocation.PythonEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => EnvPath;

        internal override string ExecutablePath => Path.Combine(InstallPath, "pythonw.exe");

        internal override string FileDialogFilter => "python|pythonw.*";

        internal override string PluginsSettingsFilePath { get => PluginSettings.PythonExecutablePath; set => PluginSettings.PythonExecutablePath = value; }

        internal PythonEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, MessageBoxEx.Show);

            // Python 3.11.4 is no longer Windows 7 compatible. If user is on Win 7 and
            // uses Python plugin they need to custom install and use v3.8.9

            var joinable = new JoinableTaskFactory(new JoinableTaskContext());

            joinable.Run(async () => {            
                await DroplexPackage.Drop(App.PythonEmbeddable, InstallPath);
            });

            PluginsSettingsFilePath = ExecutablePath;
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new PythonPlugin(filePath),
                Metadata = metadata
            };
        }
    }
}
