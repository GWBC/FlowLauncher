﻿using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.VSCode
{
    public class Settings
    {
        public bool DiscoverWorkspaces { get; set; } = true;

        public bool DiscoverMachines { get; set; } = true;

        public ObservableCollection<string> CustomWorkspaces { get; set; } = new();
    }
}
