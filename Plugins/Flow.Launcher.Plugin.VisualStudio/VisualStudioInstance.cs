using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Flow.Launcher.Plugin.VisualStudio
{
    public class VisualStudioInstance : IDisposable
    {
        public string InstanceId { get; init; }
        public Version InstallationVersion { get; init; }
        public string ExePath { get; init; }
        public string DisplayName { get; init; }
        public string RecentItemsPath { get; init; }
        public string DisplayVersion { get; init; }

        public VisualStudioInstance(JsonElement element)
        {
            var instanceId = element.GetProperty("instanceId").GetString();
            InstallationVersion = element.GetProperty("installationVersion").Deserialize<Version>();
            ExePath = element.GetProperty("productPath").GetString();
            DisplayName = element.GetProperty("displayName").GetString();
            DisplayVersion = element.GetProperty("catalog").GetProperty("productDisplayVersion").GetString();

            InstanceId = $"{InstallationVersion.Major}.0_{instanceId}";

            RecentItemsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           "Microsoft\\VisualStudio",
                                           InstanceId,
                                           "ApplicationPrivateSettings.xml");
        }

        private FileSystemWatcher watcher  = null;
        private Timer timer = null;

        public delegate void ContextChange();
        public ContextChange ContextChangeHandler = null;

        public void WatcherRecent()
        {
            timer = new(OnTimerElapsed, this, Timeout.Infinite, Timeout.Infinite);

            var path = Path.GetDirectoryName(RecentItemsPath);
            var name = Path.GetFileName(RecentItemsPath);

            watcher = new FileSystemWatcher();
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Path = path;
            watcher.Filter = name;            
            watcher.Changed += OnChange;
            watcher.EnableRaisingEvents = true;
        }

        private static void OnTimerElapsed(object state)
        {
            var vs = (VisualStudioInstance)state;
            vs.ContextChangeHandler();
        }

        private void OnChange(object sender, FileSystemEventArgs e)
        {
            //等待一段时间才触发
            timer?.Change(10 * 1000, Timeout.Infinite);
        }

        public void Dispose()
        {
            watcher?.Dispose();
        }
    }

}
