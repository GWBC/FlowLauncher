using System;
using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.VisualStudio
{
    public class Settings
    {
        public string DefaultVSId { get; set; }
        public ObservableCollection<string> CustomWorkspaces { get; set; } = new();
        public ObservableCollection<string> ExcludeCustomWorkspaces { get; set; } = new();
    }
}
