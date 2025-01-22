using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Program.Programs;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Extensions.Caching.Memory;

using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Plugin.Program
{
    public class Main : ISettingProvider, IAsyncPlugin, IPluginI18n, IContextMenu, ISavable, IAsyncReloadable,
        IDisposable
    {
        internal static Win32[] _win32s { get; set; }
        internal static UWPApp[] _uwps { get; set; }
        internal static Settings _settings { get; set; }


        internal static PluginInitContext Context { get; private set; }

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWPApp[]> _uwpStorage;

        private static readonly MemoryCacheOptions cacheOptions = new() { SizeLimit = 1560 };
        private static MemoryCache cache = new(cacheOptions);

        private static readonly string[] commonUninstallerNames =
        {
            "uninst",
            "unins000",
            "uninst000",
            "uninstall",
            "卸载"
        };

        static Main()
        {
        }

        public void Save()
        {
            if(_win32Storage != null)
                _win32Storage.SaveAsync(_win32s);

            if(_uwpStorage != null)
                _uwpStorage.SaveAsync(_uwps);
        }

        private async Task<List<Result>> QuerySystemExeAsync(string search, CancellationToken token)
        {
            var resultList = await Task.Run(() =>
            {
                try
                {
                    return _win32s.Cast<IProgram>()
                        .Concat(_uwps)
                        .AsParallel()
                        .WithCancellation(token)                       
                        .Where(p => p.Enabled)
                        .Select(p => p.Result(search, Context.API))
                        .Where(r => r?.Score > 0)
                        .ToList();
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("|Flow.Launcher.Plugin.Program.Main|Query operation cancelled");
                    return new List<Result>();
                }

            }, token);

            resultList = resultList.Any() ? resultList : new List<Result>();

            return resultList;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if(query.Search.Length == 0)
            {
                return new List<Result>();
            }

            //缓存查询结果
            //var result = await cache.GetOrCreateAsync(query.Search, async entry =>
            //{
            //    var resultList = await QuerySystemExeAsync(query.Search, token);
            //    entry.SetSize(resultList.Count);
            //    entry.SetSlidingExpiration(TimeSpan.FromMinutes(30));

            //    return resultList;
            //});

            //处理系统程序
            var result = await QuerySystemExeAsync(query.Search, token);

            //处理自定义目录
            var dirs = _settings.ProgramSources.Where(x => x.Enabled).Select(x => x.Location).ToArray();

            if (dirs.Length != 0)
            {
                var flist = Everything.EverytingFindFiles(dirs, _settings.GetSuffixes(), query.Search);
                result.AddRange(flist.Select((x) => Everything.NewResult(x, _settings.HideAppsPath)));
            }

            //计算分数
            foreach (var x in result)
            {
                var matchResult = StringMatcher.FuzzySearch(query.Search, x.Title);
                if (matchResult == null)
                {
                    continue;
                }
                
                x.Score = matchResult.Score;
                
                //x.Title = $"{x.Title} {x.Score}";
                //Context.API.LogInfo(GetType().Name, $"{x.Title} score:{matchResult.Score}");
            }

            var ret = result.AsParallel().OrderByDescending(x => x.Score)
                .Take(20).Where(Filter).ToList();

            //提高分数，让其在Explorer之前
            ret.ForEach(x => x.Score += 100);

            return ret;
        }

        private bool Filter(Result res)
        {
            if(res.Title.Length == 0)
            {
                return false;
            }

            if (!_settings.HideUninstallers) return true;            
            
            foreach(var name in commonUninstallerNames)
            {
                if(res.Title.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            await Stopwatch.NormalAsync("|Flow.Launcher.Plugin.Program.Main|Preload programs cost", async () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = await _win32Storage.TryLoadAsync(Array.Empty<Win32>());
                _uwpStorage = new BinaryStorage<UWPApp[]>("UWP");
                _uwps = await _uwpStorage.TryLoadAsync(Array.Empty<UWPApp>());
            });
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Flow.Launcher.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");

            bool cacheEmpty = !_win32s.Any() || !_uwps.Any();

            if (cacheEmpty || _settings.LastIndexTime.AddHours(30) < DateTime.Now)
            {
                _ = Task.Run(async () =>
                {
                    await IndexProgramsAsync().ConfigureAwait(false);
                    WatchProgramUpdate();
                });
            }
            else
            {
                WatchProgramUpdate();
            }

            static void WatchProgramUpdate()
            {
                Win32.WatchProgramUpdate(_settings);
                _ = UWPPackage.WatchPackageChange();
            }
        }

        public static void IndexWin32Programs()
        {
            var win32S = Win32.All(_settings);
            _win32s = win32S;
            ResetCache();
            _win32Storage.SaveAsync(_win32s);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static void IndexUwpPrograms()
        {
            var applications = UWPPackage.All(_settings);
            _uwps = applications;
            ResetCache();
            _uwpStorage.SaveAsync(_uwps);
            _settings.LastIndexTime = DateTime.Now;
        }

        public static async Task IndexProgramsAsync()
        {
            var a = Task.Run(() =>
            {
                Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                Stopwatch.Normal("|Flow.Launcher.Plugin.Program.Main|UWPProgram index cost", IndexUwpPrograms);
            });
            await Task.WhenAll(a, b).ConfigureAwait(false);
        }

        internal static void ResetCache()
        {
            var oldCache = cache;
            cache = new MemoryCache(cacheOptions);
            oldCache.Dispose();
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(Context, _settings, _win32s, _uwps);
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_program_plugin_description");
        }

        public List<Result> CreateContextMenus(Result result)
        {
            if(result.CopyText.Length == 0)
            {
                return new List<Result>();
            }

            var api = Context.API;
            var FullPath = result.CopyText;
            var ParentDirectory = Path.GetDirectoryName(FullPath);

            var contextMenus = new List<Result>
            {
                new Result
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_run_as_different_user"),
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath, WorkingDirectory = ParentDirectory, UseShellExecute = true
                        };

                        Task.Run(() => StartProcess(ShellCommand.RunAsDifferentUser, info));

                        return true;
                    },
                    IcoPath = "Images/user.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee"),
                },
                new Result
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_run_as_administrator"),
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas",
                            UseShellExecute = true
                        };

                        Task.Run(() => StartProcess(Process.Start, info));

                        return true;
                    },
                    IcoPath = "Images/cmd.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef"),
                },
                new Result
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        Context.API.OpenDirectory(ParentDirectory, FullPath);

                        return true;
                    },
                    IcoPath = "Images/folder.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe838"),
                },
            };

            return contextMenus;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<Result>();
            var program = selectedResult.ContextData as IProgram;

            if (program != null)
            {
                menuOptions = program.ContextMenus(Context.API);
                menuOptions.Add(
                     new Result
                     {
                         Title = Context.API.GetTranslation("flowlauncher_plugin_program_disable_program"),
                         Action = c =>
                         {
                             DisableProgram(program);
                             Context.API.ShowMsg(
                                 Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_success"),
                                 Context.API.GetTranslation(
                                     "flowlauncher_plugin_program_disable_dlgtitle_success_message"));
                             return false;
                         },
                         IcoPath = "Images/disable.png",
                         Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xece4"),
                     }
                 );
            }
            else
            {
                menuOptions = CreateContextMenus(selectedResult);
            }

            return menuOptions;
        }

        private static void DisableProgram(IProgram programToDelete)
        {
            if (_settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
                return;

            if (_uwps.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _uwps.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _ = Task.Run(() =>
                {
                    IndexUwpPrograms();
                });
            }
            else if (_win32s.Any(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier))
            {
                var program = _win32s.First(x => x.UniqueIdentifier == programToDelete.UniqueIdentifier);
                program.Enabled = false;
                _settings.DisabledProgramSources.Add(new ProgramSource(program));
                _ = Task.Run(() =>
                {
                    IndexWin32Programs();
                });
            }
        }

        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var title = Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_error");
                var message = string.Format(Context.API.GetTranslation("flowlauncher_plugin_program_run_failed"),
                    info.FileName);
                Context.API.ShowMsg(title, string.Format(message, info.FileName), string.Empty);
            }
        }

        public async Task ReloadDataAsync()
        {
            await IndexProgramsAsync();
        }

        public void Dispose()
        {
            Win32.Dispose();
        }
    }
}
