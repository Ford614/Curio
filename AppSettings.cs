using System.IO;
using System.Text.Json;

namespace Curio;

public class AppSettings
{
    public string DefaultUrl { get; set; } =
        "https://www.rw-designer.com/cursor-library";

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

                return JsonSerializer.Deserialize<AppSettings>(json)
                    ?? new AppSettings();
            }
        }
        catch
        {
            // 設定ファイルが壊れている場合は初期設定を使用
        }

        return new AppSettings();
    }

    public void Save()
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
}