using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.VisualStudio
{
    public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IAsyncReloadable
    {
        public PluginInitContext context;
        private VisualStudioPlugin plugin;

        private Settings settings;
        private IconProvider iconProvider;
        private Dictionary<Entry, List<int>> entryHighlightData;

        internal static string GetInstalledPath()
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key is not null)
            {
                foreach (var subKey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
                {
                    if (subKey?.GetValue("DisplayName") is not string displayName || !displayName.Contains("Everything"))
                    {
                        continue;
                    }
                    if (subKey.GetValue("UninstallString") is not string uninstallString)
                    {
                        continue;
                    }

                    if (Path.GetDirectoryName(uninstallString) is not { } uninstallDirectory)
                    {
                        continue;
                    }
                    return Path.Combine(uninstallDirectory, "Everything.exe");
                }
            }

            var location = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Join(location, "Everything", "Everything.exe");
        }

        private IEnumerable<Entry> OtherEntry()
        {            
            EverythingApi.Everything_GetMajorVersion();
            if(EverythingApi.Everything_GetLastError() != 0)
            {
                var installedLocation = GetInstalledPath();
                Process.Start(installedLocation, "-startup");

                EverythingApi.Everything_GetMajorVersion();
                if (EverythingApi.Everything_GetLastError() != 0)
                {
                    return Enumerable.Empty<Entry>();
                }
            }            

            var ws = "\"" + String.Join("\" \"", settings.CustomWorkspaces) + "\"";
            var ews = "";
            if (settings.ExcludeCustomWorkspaces.Count != 0)
            {
                ews = "!\"" + String.Join("\" !\"", settings.ExcludeCustomWorkspaces) + "\"";
            }
                
            var search = $"*.sln {ws} {ews}";

            EverythingApi.Everything_SetSearchW(search);
            EverythingApi.Everything_QueryW(true);
            int numResults = EverythingApi.Everything_GetNumResults();

            var result = new System.Text.StringBuilder(512);

            var entrys = new List<Entry>();
            for (int i = 0; i < numResults; i++)
            {
                EverythingApi.Everything_GetResultFullPathNameW(i, result, result.Capacity);
                Entry e = new Entry();
                e.Value = new Value();
                e.Value.LocalProperties = new LocalProperties();    
                e.Value.LocalProperties.FullPath = result.ToString();
                e.Value.LastAccessed = File.GetLastAccessTime(e.Path);
                entrys.Add(e);
            }

            return entrys;
        }

        //启动调用-初始化插件
        public async Task InitAsync(PluginInitContext context)
        {     
            this.context = context;
            settings = context.API.LoadSettingJsonStorage<Settings>();

            //显示变化时触发函数：OnVisibilityChanged
            //context.API.VisibilityChanged += OnVisibilityChanged;

            iconProvider = new IconProvider(context);
            plugin = await VisualStudioPlugin.Create(settings, context, iconProvider);
            entryHighlightData = new Dictionary<Entry, List<int>>();

            //解析所有的打开过的项目
            var _ = plugin.GetRecentEntries();

            EverythingApi.Load(Path.Combine(context.CurrentPluginMetadata.PluginDirectory,
                "EverythingSDK", Environment.Is64BitProcess ? "x64" : "x86"));
        }

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            if (context.CurrentPluginMetadata.Disabled)
                return;

            if (args.IsVisible)
            {
                Task.Run(async () => await plugin.GetRecentEntries());
            }
        }

        //重新加载插件
        public async Task ReloadDataAsync()
        {
            await plugin.GetVisualStudioInstances();
            iconProvider.ReloadIcons();
        }

        //查询接口
        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return null;
            }

            if (!plugin.IsVSInstalled)
            {
                return SingleResult("No installed version of Visual Studio was found");
            }

            if(query.IsReQuery)
            {
                await plugin.GetRecentEntries(token);
            }    

            if (!plugin.RecentEntries.Any())
            {
                return SingleResult("No recent items found");
            }

            entryHighlightData.Clear();
            var selectedRecentItems = plugin.RecentEntries;
            selectedRecentItems = selectedRecentItems.Concat(OtherEntry());

            selectedRecentItems = query.Search switch
            {
                string search when string.IsNullOrEmpty(query.Search) => selectedRecentItems,
                _ => selectedRecentItems.Where(e => FuzzySearch(e, query.Search))
            };

            //过滤
            selectedRecentItems = selectedRecentItems.GroupBy(e => e.Path)
                .Select(e => e.First());

            //排序
            return selectedRecentItems.OrderByDescending(e => e.Value.LastAccessed)
                                      .Select(CreateEntryResult)
                                      .ToList();
        }

        //触发右键菜单
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult.ContextData is Entry currentEntry)
            {
                return plugin.VSInstances.Select(vs =>
                {
                    return new Result
                    {
                        Title = $"Open in \"{vs.DisplayName}\" [Version: {vs.DisplayVersion}]",
                        SubTitle = vs.ExePath,
                        IcoPath = iconProvider.GetIconPath(vs),
                        Action = c =>
                        {
                            context.API.ShellRun($"\"{currentEntry.Path}\"", $"\"{vs.ExePath}\"");
                            return true;
                        }
                    };
                }).Append(new Result
                {
                    Title = $"Open in File Explorer",
                    SubTitle = currentEntry.Path,
                    IcoPath = IconProvider.Folder,
                    Action = c =>
                    {
                        context.API.OpenDirectory(Path.GetDirectoryName(currentEntry.Path), currentEntry.Path);
                        return true;
                    }
                })
                .ToList();
            }
            return null;
        }

        private static List<Result> SingleResult(string title)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = title,
                    IcoPath = IconProvider.DefaultIcon,
                }
            };
        }

        private Result CreateEntryResult(Entry e)
        {
            string iconPath  = IconProvider.DefaultIcon;
            Action action = () => context.API.ShellRun($"\"{e.Path}\"");
            if (!string.IsNullOrWhiteSpace(settings.DefaultVSId))
            {
                var instance = plugin.VSInstances.FirstOrDefault(i => i.InstanceId == settings.DefaultVSId);
                if (instance != null)
                {
                    //iconPath = iconProvider.GetIconPath(instance);
                    action = () => context.API.ShellRun($"\"{e.Path}\"", $"\"{instance.ExePath}\"");
                }
            }
            entryHighlightData.TryGetValue(e, out var highlightData);
            return new Result
            {
                Title = Path.GetFileNameWithoutExtension(e.Path),
                TitleHighlightData = highlightData,
                SubTitle = e.Value.IsFavorite ? $"★  {e.Path}" : e.Path,
                SubTitleToolTip = $"{e.Path}\n\nLast Accessed:\t{e.Value.LastAccessed:F}",
                ContextData = e,
                IcoPath =  iconPath,
                Action = c =>
                {
                    action();
                    return true;
                }
            };
        }

        private bool FuzzySearch(Entry entry, string search)
        {
            var matchResult = context.API.FuzzySearch(search, Path.GetFileNameWithoutExtension(entry.Path));
            entryHighlightData[entry] = matchResult.MatchData;
            return matchResult.IsSearchPrecisionScoreMet();
        }

        private bool TypeSearch(Entry entry, Query query, TypeKeyword typeKeyword)
        {
            var search = query.Search[typeKeyword.Keyword.Length..];
            if (string.IsNullOrWhiteSpace(search))
            {
                return entry.ItemType == typeKeyword.Type;
            }
            else
            {
                return entry.ItemType == typeKeyword.Type && FuzzySearch(entry, search);
            }
        }

        //创建设置界面
        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new UI.SettingsView(new UI.SettingsViewModel(settings, plugin, iconProvider, this));
        }

        public record struct TypeKeyword(int Type, string Keyword);
    }
}
