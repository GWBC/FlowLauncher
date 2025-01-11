using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Dictionary.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace Dictionary
{
    public class DictDownloadManager
    {
        private string dictPath;
        private bool downloading = false;
        private int downloadPercentage = 0;

        public DictDownloadManager(string dictPath)
        {
            this.dictPath = dictPath;
        }

        private async Task<bool> CheckForGoogleConnection()
        {
            try
            {
                var request = WebRequest.Create("https://google.com/generate_204");
                request.Timeout = 2000;
                await request.GetResponseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> GetDownloadUrl()
        {
            bool shouldUseMirror = !await CheckForGoogleConnection();
            if (shouldUseMirror)
                return "https://github.moeyy.xyz/https://github.com/GWBC/ecdict/releases/download/1.0.0/ecdict-ultimate-sqlite.zip";
            else
                return "https://github.com/GWBC/ecdict/releases/download/1.0.0/ecdict-ultimate-sqlite.zip";
        }

        public async void PerformDownload()
        {
            downloading = true;

            var url = await GetDownloadUrl();
            var path = Path.GetDirectoryName(dictPath);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var client = new WebClient();

            client.DownloadProgressChanged += delegate (object sender, DownloadProgressChangedEventArgs e)
            {
                downloadPercentage = e.ProgressPercentage;
            };

            var tmpDictFileLoc = dictPath + ".download";
            await client.DownloadFileTaskAsync(new Uri(url), tmpDictFileLoc).ConfigureAwait(false);

            await Task.Run(() => ZipFile.ExtractToDirectory(tmpDictFileLoc, Path.GetDirectoryName(dictPath)));

            File.Delete(tmpDictFileLoc);

            downloading = false;
        }

        public async Task<List<Result>> HandleQueryAsync(Query query)
        {
            if (downloading)
            {
                var progress = "";
                if (downloadPercentage != 0) progress = $"{downloadPercentage} %";
                return new List<Result> { new Result() {
                    Title = $"{Resources.DownloadingDB}{progress}",
                    SubTitle = $"{Resources.Refresh}",
                    IcoPath = "Images\\icon.png",
                    Action = (e) => {
                        Main.Context.API.ChangeQuery("d downloading" + new string('.',new Random().Next(0,10)));
                        return false;
                    }
                }};
            }
            else
            {
                return new List<Result> { new Result() {
                    Title = $"{Resources.DictionaryDes}",
                    SubTitle = $"{Resources.DownloadTo} {dictPath} (~230MB)",
                    IcoPath = "Images\\icon.png",
                    Action = (e) =>
                    {
                        if(!downloading) PerformDownload();
                        Main.Context.API.ChangeQuery($"tr {Resources.Progress}");
                        return false;
                    }
                }};
            }
        }

        public bool NeedsDownload()
        {
            return !File.Exists(dictPath);
        }
    }
}
