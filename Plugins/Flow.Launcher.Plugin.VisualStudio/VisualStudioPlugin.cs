using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.VisualStudio
{
    public class VisualStudioPlugin
    {
        private readonly PluginInitContext context;
        private readonly Settings settings;
        private readonly IconProvider iconProvider;
        private readonly ConcurrentDictionary<string, Entry> recentEntries = new();
        private readonly ConcurrentBag<VisualStudioInstance> vsInstances = new();

        public static async Task<VisualStudioPlugin> Create(Settings settings, PluginInitContext context, IconProvider iconProvider)
        {
            var plugin = new VisualStudioPlugin(settings, context, iconProvider);
            await plugin.GetVisualStudioInstances();
            return plugin;
        }
        private VisualStudioPlugin(Settings settings, PluginInitContext context, IconProvider iconProvider)
        {
            this.context = context;
            this.settings = settings;
            this.iconProvider = iconProvider;
        }

        public bool IsVSInstalled => !vsInstances.IsEmpty;
        public IEnumerable<Entry> RecentEntries => recentEntries.Select(kvp => kvp.Value);
        public IEnumerable<VisualStudioInstance> VSInstances => vsInstances;

        public async Task GetVisualStudioInstances()
        {
            using var vswhere = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio\\Installer\\vswhere.exe"),
                Arguments = "-sort -format json -utf8 -prerelease",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });


            using var doc = await JsonDocument.ParseAsync(vswhere.StandardOutput.BaseStream);

            foreach(var vs in vsInstances)
            {
                vs.Dispose();
            }

            vsInstances.Clear();

            int count;
            if (doc.RootElement.ValueKind != JsonValueKind.Array || (count = doc.RootElement.GetArrayLength()) < 1)
                return;

            Parallel.For(0, count, index => {
                var vs = new VisualStudioInstance(doc.RootElement[index]);

                //文件变化后更新项目
                vs.ContextChangeHandler = () =>
                {
                    Task.Run(async () => await GetRecentEntries());
                };

                vs.WatcherRecent();

                vsInstances.Add(vs);
             });
        }

        public async Task GetRecentEntries(CancellationToken token = default)
        {
            recentEntries.Clear();

            foreach (var vs in VSInstances)
            {
                var entries = await GetRecentEntriesFromInstance(vs, token);

                Parallel.ForEach(entries, entry => {
                    
                    //过滤掉无效项目
                    if (!entry.Key.FileExists())
                    {
                        return;
                    }

                    //过滤掉非解决方案
                    if(Path.GetExtension(entry.Key) != ".sln")
                    {
                        return;
                    }

                    if(recentEntries.TryGetValue(entry.Key, out Entry v))
                    {
                        if(v.Value.LastAccessed  > entry.Value.LastAccessed)
                        {
                            return;
                        }
                    }

                    recentEntries[entry.Key] = entry;
                });
            }
        }

        public void Save()
        {
            context.API.SaveSettingJsonStorage<Settings>();
        }

        private static async Task<Entry[]> GetRecentEntriesFromInstance(VisualStudioInstance vs, CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(vs.RecentItemsPath, FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(fileStream, new XmlReaderSettings() { Async = true });
            await reader.MoveToContentAsync();

            var correctElement = false;
            while (await reader.ReadAsync())
            {
                if (correctElement
                    && reader.NodeType == XmlNodeType.Element
                    && reader.HasAttributes
                    && reader.GetAttribute(0) == "value")
                {
                    await reader.ReadAsync();
                    break;
                }
                if (reader.NodeType == XmlNodeType.Element
                    && reader.HasAttributes
                    && reader.GetAttribute(0) == "CodeContainers.Offline")
                {
                    correctElement = true;
                }
            }

            var json = await reader.GetValueAsync();

            try
            {
                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                return await JsonSerializer.DeserializeAsync<Entry[]>(memoryStream, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentNullException)
            {
                return Array.Empty<Entry>();
            }
        }

        private async Task UpdateVisualStudioInstances()
        {
            await Parallel.ForEachAsync(VSInstances, async (vs, ct) =>
            {
                using var memoryStream = new MemoryStream();

                var json = JsonSerializer.SerializeAsync(memoryStream, RecentEntries.ToArray(), cancellationToken: ct);
                ///Open xml document
                using var fileStream = new FileStream(vs.RecentItemsPath, FileMode.Open, FileAccess.ReadWrite);
                var root = await XDocument.LoadAsync(fileStream, LoadOptions.None, ct);
                var recent = root.Element("content")
                                 .Element("indexed")
                                 .Elements("collection")
                                 .Where(e => (string)e.Attribute("name") == "CodeContainers.Offline")
                                 .First()
                                 .Element("value");
                ///Make sure Json is serialized
                await json;

                //write new entries to xml value
                memoryStream.Position = 0;
                using var streamReader = new StreamReader(memoryStream, Encoding.UTF8);
                recent.Value = await streamReader.ReadToEndAsync(ct);

                //save file
                fileStream.SetLength(0);
                using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
                await root.SaveAsync(streamWriter, SaveOptions.DisableFormatting, ct);
            });
        }
    }
}
