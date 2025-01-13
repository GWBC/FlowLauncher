using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Dictionary.Properties;

namespace Dictionary
{
    public class Main : IAsyncPlugin, IPluginI18n, ISettingProvider, IResultUpdated, ISavable
    {
        private ECDict ecdict = null!;
        private WordCorrection wordCorrection = null!;
        private Iciba iciba = null!;
        internal static PluginInitContext Context { get; private set; } = null!;
        private Settings settings = null!;
        private DictDownloadManager dictDownloadManager = null!;

        private readonly string ecdictLocation = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Flow.Dictionary\ultimate.db");
        private readonly string configLocation = Environment.ExpandEnvironmentVariables(@"%AppData%\FlowLauncher\Settings\Plugins\Flow.Dictionary\config.json");

        private string QueryWord { get; set; } = "";

        public event ResultUpdatedEventHandler? ResultsUpdated;

        public Control CreateSettingPanel()
        {
            return new DictionarySettings(settings);
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            WebsterAudio.Api = context.API;

            await Task.Run(() =>
            {
                settings = Context.API.LoadSettingJsonStorage<Settings>();
                settings.Ctx = context;

                iciba = new Iciba();
                ecdict = new ECDict(ecdictLocation);
                dictDownloadManager = new DictDownloadManager(ecdictLocation);

                var currentPath = context.CurrentPluginMetadata.PluginDirectory;
                wordCorrection = new WordCorrection(currentPath + "/dicts/frequency_dictionary_en_82_765.txt", settings.MaxEditDistance);
            });
        }

        private Result MakeResultItem(string title, string subtitle, string? extraAction = null, string? word = null)
        {
            string query = (word ?? QueryWord);
            string GetWord() => query.Replace("!", "");

            void Copy(ActionContext e)
            {
                try
                {
                    Clipboard.SetDataObject(GetWord());
                }
                catch (ExternalException ee)
                {
                    Context.API.ShowMsg("Copy failed, please try later", ee.Message);
                }
            }

            bool ReadWordIfNeeded(ActionContext e)
            {
                if (string.IsNullOrEmpty(settings.MerriamWebsterKey))
                {
                    return false;
                }

                if (!e.SpecialKeyState.CtrlPressed)
                {
                    return false;
                }

                _ = WebsterAudio.Play(GetWord(), settings.MerriamWebsterKey);
                return true;
            }

            Func<ActionContext, bool> actionFunc;
            actionFunc = e =>
            {
                Copy(e);
                ReadWordIfNeeded(e);
                return false;
            };

            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = "Images\\icon.png",
                Action = actionFunc
            };
        }

        private Result MakeWordResult(WordInformation wordInformation) =>
            MakeResultItem(wordInformation.Word, 
                (wordInformation.Phonetic != "" ? ("/" + wordInformation.Phonetic + "/ ") : "") +
                 wordInformation.Translation.Replace("\n", "; "),
                "!", wordInformation.Word);

        private class WordEqualityComparer : IEqualityComparer<Result>
        {
            public static WordEqualityComparer Instance { get; }= new();

            public bool Equals(Result? x, Result? y)
            {
                if (x != null && x.Equals(y))
                {
                    return true;
                }

                return x?.Title == y?.Title;
            }

            public int GetHashCode(Result obj)
            {
                return obj.Title.GetHashCode();
            }
        }

        // First-level query.
        // English -> Chinese, supports fuzzy search.
        private async ValueTask<List<Result>> FirstLevelQueryAsync(Query query, CancellationToken token)
        {
            var queryWord = query.Search;
            var results = new HashSet<Result>(WordEqualityComparer.Instance);

            // Pull fully match first.
            var fullMatch = ecdict.Query(query.Search);
            if (fullMatch != null)
            {
                results.Add(MakeWordResult(fullMatch));
            }                

            token.ThrowIfCancellationRequested();
            ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs
            {
                Results = results.ToList(), Query = query
            });

            // Then fuzzy search results. (since it's usually only a few)
            var suggestions = wordCorrection.Correct(queryWord);
            token.ThrowIfCancellationRequested();

            await foreach (var word in ecdict.QueryRange(suggestions.Select(x => x.term), token).Select(MakeWordResult).ConfigureAwait(false))
            {
                results.Add(word);
            }

            token.ThrowIfCancellationRequested();
            ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs
            {
                Results = results.ToList(), Query = query
            });

            foreach (var word in ecdict.QueryBeginningWith(queryWord).Select(MakeWordResult))
            {
                results.Add(word);
            }

            return results.ToList();
        }

        // Chinese translation of a word.
        // English -> Synonyms
        // Fuzzy search disabled.
        // Internet access needed.
        private async ValueTask<List<Result>> ChineseQueryAsync(Query query, CancellationToken token)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(settings.ICIBAToken))
            {
                return results;
            }

            var queryWord = query.Search; 

            var translations = await iciba.QueryAsync(queryWord,
                settings.ICIBAToken, token).ConfigureAwait(false);

            if (translations.Count == 0)
            {
                results.Add(MakeResultItem($"{Resources.NoFound}", queryWord));
            }
            else
            {
                results.AddRange(translations.Select(translation => MakeResultItem(translation, queryWord, "!", translation)));
            }

            return results;
        }

        private bool IsChinese(string cn)
        {
            foreach (var c in cn)
            {
                var cat = char.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.OtherLetter)
                    return true;
            }

            return false;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (dictDownloadManager.NeedsDownload())
            {
                return await dictDownloadManager.HandleQueryAsync(query).ConfigureAwait(false);
            }

            var queryWord = query.Search;
            if (queryWord == "")
            {
                return new List<Result>();
            }

            QueryWord = queryWord;
            
            if (IsChinese(queryWord))
            {
                return await ChineseQueryAsync(query, token);
            }

            return await FirstLevelQueryAsync(query, token).ConfigureAwait(false);
        }
        public void Save()
        {
            settings?.Save();
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.Title;          
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.SubTitle;
        }
    }
}
