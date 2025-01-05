using System.Collections.Generic;
using Droplex;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin;
using System.IO;
using Flow.Launcher.Core.Plugin;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal class TypeScriptV2Environment : AbstractPluginEnvironment
    {
        internal override string Language => AllowedLanguage.TypeScriptV2;

        internal override string EnvName => DataLocation.NodeEnvironmentName;

        internal override string EnvPath => Path.Combine(DataLocation.PluginEnvironmentsPath, EnvName);

        internal override string InstallPath => EnvPath;

        internal override string ExecutablePath => Path.Combine(InstallPath, "node-v22.12.0-win-x64\\node.exe");

        internal override string PluginsSettingsFilePath { get => PluginSettings.NodeExecutablePath; set => PluginSettings.NodeExecutablePath = value; }

        internal TypeScriptV2Environment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings) : base(pluginMetadataList, pluginSettings) { }

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, MessageBoxEx.Show);

            var joinable = new JoinableTaskFactory(new JoinableTaskContext());

            joinable.Run(async ()=> {
                await DroplexPackage.Drop(App.Nodejs, InstallPath);
            });
           

            PluginsSettingsFilePath = ExecutablePath;
        }

        internal override PluginPair CreatePluginPair(string filePath, PluginMetadata metadata)
        {
            return new PluginPair
            {
                Plugin = new NodePluginV2(filePath),
                Metadata = metadata
            };
        }
    }
}
