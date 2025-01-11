using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dictionary
{
    public partial class DictionarySettings
    {
        public Settings Settings { get; }
        private Timer saveTimer { get; init; }

        public DictionarySettings(Settings settings)
        {
            saveTimer = new(SaveTimer, null, Timeout.Infinite, Timeout.Infinite);
            Settings = settings;
            InitializeComponent();
            ICIBAToken.Password = Settings.ICIBAToken;
            AudioToken.Password = Settings.MerriamWebsterKey;
        }

        private void SaveTimer(object? state)
        {
            Settings.Save(); 
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) 
            { 
                UseShellExecute = true
            });

            e.Handled = true;
        }

        private void ICIBAToken_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Settings.ICIBAToken = ICIBAToken.Password;

            //保存配置
            saveTimer?.Change(2 * 1000, Timeout.Infinite);
        }

        private void AudoToken_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Settings.MerriamWebsterKey = AudioToken.Password;

            //保存配置
            saveTimer?.Change(2 * 1000, Timeout.Infinite);
        }
    }
}
