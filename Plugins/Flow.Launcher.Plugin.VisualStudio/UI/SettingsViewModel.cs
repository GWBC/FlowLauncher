using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.VisualStudio.UI
{
    public class SettingsViewModel : BaseModel
    {
        public Settings settings { get; set; }
        private readonly VisualStudioPlugin plugin;
        private readonly IconProvider iconProvider;
        private readonly IAsyncReloadable reloadable;

        private VisualStudioModel selectedVSInstance;

        public SettingsViewModel(Settings settings, VisualStudioPlugin plugin, 
            IconProvider iconProvider, IAsyncReloadable reloadable)
        {
            this.settings = settings;
            this.plugin = plugin;
            this.iconProvider = iconProvider;
            this.reloadable = reloadable;
            SetupVSInstances(settings, plugin);
        }

        public List<VisualStudioModel> VSInstances { get; set; }
        public VisualStudioModel SelectedVSInstance
        {
            get => selectedVSInstance; 
            set
            {
                selectedVSInstance = value;
                settings.DefaultVSId = selectedVSInstance.InstanceId;
                OnPropertyChanged();
            }
        }

        private void SetupVSInstances(Settings settings, VisualStudioPlugin plugin)
        {
            VSInstances = new List<VisualStudioModel>(plugin.VSInstances.Select(vs => 
            {
                return new VisualStudioModel
                {
                    IconPath = iconProvider.GetIconPath(vs),
                    Name = $"{vs.DisplayName} [Version: {vs.DisplayVersion}]",
                    InstanceId = vs.InstanceId,
                };
            }));
            VSInstances.Insert(0, new VisualStudioModel
            {
                IconPath = iconProvider.Windows,
                Name = "Let Windows Decide (Default)",
                InstanceId = null,
            });
            SelectedVSInstance = VSInstances.FirstOrDefault(i => i.InstanceId == settings.DefaultVSId);
        }

        public async Task RefreshInstances()
        {
            await reloadable.ReloadDataAsync();
            SetupVSInstances(settings, plugin);
            OnPropertyChanged(nameof(VSInstances));
        }

        public void Save()
        {
            plugin.Save();
        }

        public void AddWorkspace(string path)
        {
            settings.CustomWorkspaces.Add(path);
        }

        public void DelWorkspace(string path)
        {
            settings.CustomWorkspaces.Remove(path);
        }

        public void AddEWorkspace(string path)
        {
            settings.ExcludeCustomWorkspaces.Add(path);
        }

        public void DelEWorkspace(string path)
        {
            settings.ExcludeCustomWorkspaces.Remove(path);
        }

        public class VisualStudioModel
        {
            public string IconPath { get; init; }
            public string Name { get; init; }
            public string InstanceId { get; init; }
        }
    }
}
