using System;
using System.IO;
using System.Text.Json;

namespace Curio
{
    public class AppSettings
    {
        public string DefaultUrl { get; set; } = "https://www.rw-designer.com/cursor-library";
        public string Theme { get; set; } = "System"; // "System", "Light", "Dark"
        public string UIStyle { get; set; } = "Modern"; // "Modern", "XP" (Windows XP風)
        public string Language { get; set; } = "ja-JP"; // "ja-JP", "en-US"
        public string StorageDirectory { get; set; } = string.Empty;

        private static readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Curio");

        private static readonly string SettingsPath =
            Path.Combine(SettingsDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    settings.Normalize();
                    return settings;
                }
            }
            catch
            {
                // If settings file is corrupt, return defaults
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);

                Normalize();
                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                string temporaryPath = SettingsPath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, SettingsPath, overwrite: true);
            }
            catch
            {
            }
        }

        private void Normalize()
        {
            if (!string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Theme, "Dark", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Theme, "System", StringComparison.OrdinalIgnoreCase))
                Theme = "System";

            if (!string.Equals(UIStyle, "XP", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(UIStyle, "Modern", StringComparison.OrdinalIgnoreCase))
                UIStyle = "Modern";

            if (!string.Equals(Language, "en-US", StringComparison.OrdinalIgnoreCase))
                Language = "ja-JP";

            if (!string.IsNullOrWhiteSpace(StorageDirectory) && !Path.IsPathFullyQualified(StorageDirectory))
                StorageDirectory = string.Empty;
        }
    }
}
