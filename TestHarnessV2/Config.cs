using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace TestHarnessV2
{
    internal sealed class AppSettings
    {
        public string GameUrl { get; set; } = "https://stagingretail.gameonstudios.bet/";
        public double DefaultZoomFactor { get; set; } = 1.0;
        public List<string> VideoRoots { get; set; } = new();

        /// <summary>When true, WebView Reload runs at each configured local time once per day.</summary>
        public bool ScheduledUrlRefreshEnabled { get; set; } = true;

        /// <summary>Local times of day as "HH:mm" (24h), e.g. "01:04", "07:15".</summary>
        public List<string> ScheduledUrlRefreshTimes { get; set; } = new() { "01:04", "07:15" };

        /// <summary>Refresh if now is in [scheduled, scheduled + this many minutes). Should be ≥ polling interval.</summary>
        public int ScheduledUrlRefreshWindowMinutes { get; set; } = 2;

        /// <summary>When true, monitor the local roulette websocket for missing round_result events.</summary>
        public bool WebSocketMonitorEnabled { get; set; } = true;

        /// <summary>Dedicated local websocket endpoint used for display monitoring.</summary>
        public string WebSocketUrl { get; set; } = "ws://localhost:5005/ws/roulette";

        /// <summary>Seconds to wait before retrying a failed websocket connection.</summary>
        public int WebSocketReconnectDelaySeconds { get; set; } = 5;

        /// <summary>Seconds without round_result after the game has started before refresh is requested.</summary>
        public int RoundResultTimeoutSeconds { get; set; } = 90;

        /// <summary>Prevents repeated reloads while the frontend remains unhealthy.</summary>
        public int RefreshCooldownSeconds { get; set; } = 300;

        /// <summary>Only arm the timeout after at least one round_result has been observed.</summary>
        public bool RequireFirstRoundResultBeforeRefresh { get; set; } = true;
    }

    internal static class AppConfig
    {
        private static readonly object _settingsLock = new object();
        private static AppSettings _settings;

        public static AppSettings Current
        {
            get
            {
                lock (_settingsLock)
                {
                    if (_settings == null)
                        _settings = LoadFromPath(logLoaded: true);
                    return _settings;
                }
            }
        }

        /// <summary>Re-reads settings.json and replaces the cached settings (quiet unless load fails).</summary>
        public static void ReloadFromDisk()
        {
            lock (_settingsLock)
            {
                _settings = LoadFromPath(logLoaded: false);
            }
        }

        private static AppSettings LoadFromPath(bool logLoaded)
        {
            try
            {
                string basePath = Application.StartupPath;
                string configPath = Path.Combine(basePath, "settings.json");
                if (!File.Exists(configPath))
                {
                    if (logLoaded)
                        Logger.Log($"[CONFIG] settings.json not found at {configPath}, using built-in defaults.");
                    return new AppSettings();
                }

                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();

                if (logLoaded)
                    Logger.Log($"[CONFIG] Loaded settings from {configPath}");
                return settings;
            }
            catch (Exception ex)
            {
                Logger.LogError("[CONFIG] Failed to load settings.json, using defaults.", ex);
                return new AppSettings();
            }
        }
    }
}
