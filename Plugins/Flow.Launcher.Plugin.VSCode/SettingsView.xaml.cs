using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.VSCode.WorkspacesHelper;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace Flow.Launcher.Plugin.VSCode
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private readonly PluginInitContext _context;
        private readonly Settings _settings;

        public SettingsView(PluginInitContext context, Settings settings)
        {
            _context = context;
            DataContext = _settings = settings;

            InitializeComponent();
        }

        public void Save(object sender = null, RoutedEventArgs e = null) => _context.API.SaveSettingJsonStorage<Settings>();

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ws in listView.SelectedItems.Cast<string>().ToArray())
            {
                _settings.CustomWorkspaces.Remove(ws);
            }
            Save();
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uri = addUri.Text;

                if (string.IsNullOrEmpty(uri))
                {
                    using (var folderDialog = new Forms.FolderBrowserDialog())
                    {
                        if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                        {
                            uri = folderDialog.SelectedPath;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                //不转换为URI
                // System.Uri fails to parse vscode-remote://XXX+YYY URIs, skip them
                //var type = ParseVSCodeUri.GetTypeWorkspace(uri).TypeWorkspace;
                //if (!type.HasValue || type.Value == TypeWorkspace.Local)
                //{
                //    // Converts file paths to proper URI
                //    uri = new Uri(uri).AbsoluteUri;
                //}

                addUri.Clear();

                if (_settings.CustomWorkspaces.Contains(uri))
                {
                    return;
                }
                _settings.CustomWorkspaces.Add(uri);
                Save();
            }
            catch (Exception ex)
            {
                _context.API.ShowMsgError("Error", ex.Message);
            }
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
