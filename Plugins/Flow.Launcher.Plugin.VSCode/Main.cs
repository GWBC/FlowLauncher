﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Flow.Launcher.Plugin.VSCode
{
    using Flow.Launcher.Plugin;
    using Properties;
    using RemoteMachinesHelper;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Windows.Controls;
    using VSCodeHelper;
    using WorkspacesHelper;

    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu
    {
        internal static PluginInitContext _context { get; private set; }

        private static Settings _settings;

        private VSCodeInstance defaultInstalce;

        private readonly VSCodeWorkspacesApi _workspacesApi = new();

        private readonly VSCodeRemoteMachinesApi _machinesApi = new();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var workspaces = new List<VSCodeWorkspace>();

            // User defined extra workspaces
            if (defaultInstalce != null)
            {
                foreach(var path in _settings.CustomWorkspaces)
                {   
                    try
                    {
                        var paths = Directory.GetDirectories(path);
                        foreach (var p in paths)
                        {
                            var vs = VSCodeWorkspacesApi.ParseVSCodeUri(new Uri(p).ToString(), defaultInstalce);
                            if(vs == null)
                            {
                                continue;
                            }

                            workspaces.Add(vs);
                        }
                    }
                    catch
                    {

                    }
                }
            }

            // Search opened workspaces
            if (_settings.DiscoverWorkspaces)
            {
                workspaces.AddRange(_workspacesApi.Workspaces);
            }

            // Simple de-duplication
            results.AddRange(workspaces.Distinct()
                .Select(CreateWorkspaceResult)
            );

            // Search opened remote machines
            if (_settings.DiscoverMachines)
            {
                _machinesApi.Machines.ForEach(a =>
                {
                    var title = "";

                    if (!string.IsNullOrEmpty(a.User))
                    {
                        title += $"{a.User}@";
                    }

                    if(!string.IsNullOrEmpty(a.HostName))
                    {
                        title += $"{a.HostName}";
                    }
                    else
                    {
                        title += $"{a.Host}";
                    }

                    title += $":{a.Port}";

                    var tooltip = Resources.SSHRemoteMachine;

                    results.Add(new Result
                    {
                        Title = title,
                        SubTitle = Resources.SSHRemoteMachine,
                        Icon = a.VSCodeInstance.RemoteIcon,
                        TitleToolTip = tooltip,
                        Action = c =>
                        {
                            bool hide;
                            try
                            {
                                var process = new ProcessStartInfo
                                {
                                    FileName = a.VSCodeInstance.ExecutablePath,
                                    UseShellExecute = true,
                                    Arguments =
                                        $"--new-window --enable-proposed-api ms-vscode-remote.remote-ssh --remote ssh-remote+{((char)34) + a.Host + ((char)34)}",
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                };
                                Process.Start(process);

                                hide = true;
                            }
                            catch (Win32Exception)
                            {
                                var name = $"{_context.CurrentPluginMetadata.Name}";
                                string msg = Resources.OpenFail;
                                _context.API.ShowMsg(name, msg, string.Empty);
                                hide = false;
                            }

                            return hide;
                        },
                        ContextData = a,
                    });
                });
            }

            if (query.ActionKeyword == string.Empty ||
                (query.ActionKeyword != string.Empty && query.Search != string.Empty))
            {
                results = results.Where(r =>
                {
                    r.Score = _context.API.FuzzySearch(query.Search, r.Title).Score;
                    return r.Score > 0;
                }).ToList();
            }

            results = results.OrderByDescending(e =>
            {               
                try
                {
                    var ctx = (VSCodeWorkspace)e.ContextData;
                    return ctx.LastWriteTime.Second;
                }
                catch
                {
                    return 0;
                }

            }).ToList();

            return results;
        }

        private Result CreateWorkspaceResult(VSCodeWorkspace ws)
        {
            var title = $"{ws.FolderName}";
            var typeWorkspace = ws.WorkspaceTypeToString();

            if (ws.TypeWorkspace != TypeWorkspace.Local)
            {
                title = ws.Lable != null
                    ? $"{ws.Lable}"
                    : $"{title}{(ws.ExtraInfo != null ? $" - {ws.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
            }

            var workspace = $"{Resources.Workspace}";
            var localTip = $": {SystemPath.RealPath(ws.RelativePath)}";

            var subTitle = $"{workspace} {localTip}";
            var toolTip = $"{Resources.Project}{title}\n{Resources.LastWriteTime}{ws.LastWriteTime}";
            
            return new Result
            {
                Title = title,
                SubTitle = subTitle,
                Icon = ws.VSCodeInstance.WorkspaceIcon,
                TitleToolTip = toolTip,
                Action = c =>
                {
                    try
                    {
                        var modifierKeys = c.SpecialKeyState.ToModifierKeys();
                        if (modifierKeys == System.Windows.Input.ModifierKeys.Control)
                        {
                            _context.API.OpenDirectory(SystemPath.RealPath(ws.RelativePath));
                            return true;
                        }

                        var process = new ProcessStartInfo
                        {
                            FileName = ws.VSCodeInstance.ExecutablePath,
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                        };
                        process.ArgumentList.Add("--folder-uri");
                        process.ArgumentList.Add(ws.Path);

                        Process.Start(process);
                        return true;
                    }
                    catch (Win32Exception)
                    {
                        var name = $"{_context.CurrentPluginMetadata.Name}";
                        string msg = Resources.OpenFail;
                        _context.API.ShowMsg(name, msg, string.Empty);
                    }

                    return false;
                },
                ContextData = ws,
            };
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();

            VSCodeInstances.LoadVSCodeInstances();

            // Prefer stable version, or the first one we got
            defaultInstalce = VSCodeInstances.Instances.Find(e => e.VSCodeVersion == VSCodeVersion.Stable) ??
                              VSCodeInstances.Instances.FirstOrDefault();
        }

        public Control CreateSettingPanel() => new SettingsView(_context, _settings);

        public string GetTranslatedPluginTitle()
        {
            return Resources.PluginTitle;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.PluginDescription;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            List<Result> results = new();
            if (selectedResult.ContextData is VSCodeWorkspace ws && ws.TypeWorkspace == TypeWorkspace.Local)
            {
                results.Add(new Result
                {
                    Title = Resources.OpenFolder,
                    SubTitle = Resources.OpenFolderSubTitle,
                    Icon = ws.VSCodeInstance.WorkspaceIcon,
                    TitleToolTip = Resources.OpenFolderSubTitle,
                    Action = c =>
                    {
                        _context.API.OpenDirectory(SystemPath.RealPath(ws.RelativePath));
                        return true;
                    },
                    ContextData = ws,
                });
            }

            return results;
        }
    }
}
