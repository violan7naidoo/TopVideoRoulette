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
    }

    internal static class AppConfig
    {
        private static readonly Lazy<AppSettings> _current = new Lazy<AppSettings>(Load, true);

        public static AppSettings Current => _current.Value;

        private static AppSettings Load()
        {
            try
            {
                string basePath = Application.StartupPath;
                string configPath = Path.Combine(basePath, "settings.json");
                if (!File.Exists(configPath))
                {
                    Logger.Log($"[CONFIG] settings.json not found at {configPath}, using built-in defaults.");
                    return new AppSettings();
                }

                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();

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

