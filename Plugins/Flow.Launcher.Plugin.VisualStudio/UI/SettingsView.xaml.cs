using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace Flow.Launcher.Plugin.VisualStudio.UI
{
    public partial class SettingsView : UserControl
    {
        private readonly SettingsViewModel viewModel;
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = this.viewModel = viewModel;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await DisableSenderWhileAwaiting(sender, viewModel.RefreshInstances);

        private static async Task DisableSenderWhileAwaiting(object sender, Func<Task> action)
        {
            var element = (UIElement)sender;
            element.IsEnabled = false;
            await action();
            element.IsEnabled = true;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewModel.Save();
        }

        private void Button_Click_Add(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new Forms.FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    viewModel.AddWorkspace(folderDialog.SelectedPath);
                    viewModel.Save();
                }
            }
        }

        private void Button_Click_Delete(object sender, RoutedEventArgs e)
        {
            foreach (var ws in listView.SelectedItems.Cast<string>().ToArray())
            {
                viewModel.DelWorkspace(ws);
            }

            viewModel.Save();
        }

        private void Button_Click_EAdd(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new Forms.FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    viewModel.AddEWorkspace(folderDialog.SelectedPath);
                    viewModel.Save();
                }
            }
        }

        private void Button_Click_EDelete(object sender, RoutedEventArgs e)
        {
            foreach (var ws in listViewExclude.SelectedItems.Cast<string>().ToArray())
            {
                viewModel.DelEWorkspace(ws);
            }

            viewModel.Save();
        }
    }
}
