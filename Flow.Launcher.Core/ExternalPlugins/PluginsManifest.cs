using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public static class PluginsManifest
    {
        //全球访问: 使用 cdn.jsdelivr.net，它会自动选择最佳的 CDN 节点
        //北美/欧洲: 推荐 fastly.jsdelivr.net
        //东欧/俄罗斯/独联体: 推荐 gcore.jsdelivr.net
        //private static readonly CommunityPluginStore mainPluginStore =
        //    new("https://raw.githubusercontent.com/Flow-Launcher/Flow.Launcher.PluginsManifest/plugin_api_v2/plugins.json",
        //        "https://fastly.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
        //        "https://gcore.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
        //        "https://cdn.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json");

        private static readonly CommunityPluginStore mainPluginStore =
            new("https://cdn.jsdelivr.net/gh/Flow-Launcher/Flow.Launcher.PluginsManifest@plugin_api_v2/plugins.json",
                "https://cdn.jsdelivr.net/gh/GWBC/FlowlauncherPlug@main/plugins.json");

        private static readonly SemaphoreSlim manifestUpdateLock = new(1);

        private static DateTime lastFetchedAt = DateTime.MinValue;
        private static TimeSpan fetchTimeout = TimeSpan.FromSeconds(60);

        public static List<UserPlugin> UserPlugins { get; private set; }

        public static async Task UpdateManifestAsync(CancellationToken token = default, bool usePrimaryUrlOnly = false, int retry = 0)
        {
            try
            {
                await manifestUpdateLock.WaitAsync(token).ConfigureAwait(false);

                if (UserPlugins == null || usePrimaryUrlOnly || DateTime.Now.Subtract(lastFetchedAt) >= fetchTimeout)
                {
                    var results = await mainPluginStore.FetchAsync(token, usePrimaryUrlOnly).ConfigureAwait(false);
                    UserPlugins = results;

                    if (results != null && results.Count > 0)
                    {
                        lastFetchedAt = DateTime.Now;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception($"|PluginsManifest.{nameof(UpdateManifestAsync)}|Http request failed", e);
                if(retry >= 3)
                {
                    return;
                }

                await UpdateManifestAsync(token, usePrimaryUrlOnly, retry + 1);
            }
            finally
            {
                manifestUpdateLock.Release();
            }
        }
    }
}
