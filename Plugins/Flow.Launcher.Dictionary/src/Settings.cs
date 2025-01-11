using System.IO;
using System.Text.Json;
using Flow.Launcher.Plugin;

namespace Dictionary
{
    public class Settings
    {
        public PluginInitContext Ctx;
        public string ICIBAToken { get; set; } = "BEBC0A981CB63ED5198597D732BD8956";
        public string MerriamWebsterKey { get; set; } = "";
        public int MaxEditDistance { get; set; } = 3;

        public void Save()
        {
            Ctx.API.SaveSettingJsonStorage<Settings>();
        }
    }
}
