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
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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

                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
            }
        }
    }
}