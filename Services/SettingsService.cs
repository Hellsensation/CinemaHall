using System.IO;
using System.Text.Json;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Загрузка и сохранение настроек приложения в %LocalAppData%\CinemaHall\settings.json.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CinemaHall",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Загружает настройки; при отсутствии или ошибке возвращает значения по умолчанию.</summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Повреждённый файл настроек игнорируем — начинаем с пустых значений
        }

        return new AppSettings();
    }

    /// <summary>Сохраняет настройки на диск.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
