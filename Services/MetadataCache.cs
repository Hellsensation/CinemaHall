using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Дисковый кэш метаданных: %LocalAppData%\CinemaHall\cache.
/// Для каждого элемента каталога хранится папка meta.json + poster.jpg.
/// Кэш снижает расход квоты API (200 запросов/сутки) и ускоряет запуск.
/// </summary>
public class MetadataCache
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CinemaHall",
        "cache");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Папка кэша для конкретного элемента (по хэшу пути).</summary>
    private static string DirFor(MediaItem item)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Path)));
        return Path.Combine(CacheRoot, hash);
    }

    /// <summary>
    /// Пытается загрузить метаданные из кэша в переданный элемент.
    /// Возвращает true, если метаданные найдены.
    /// </summary>
    public bool TryLoad(MediaItem item)
    {
        var metaPath = Path.Combine(DirFor(item), "meta.json");
        try
        {
            if (!File.Exists(metaPath))
            {
                return false;
            }

            var cached = JsonSerializer.Deserialize<MediaItem>(File.ReadAllText(metaPath), JsonOptions);
            if (cached is null)
            {
                return false;
            }

            // Переносим метаданные, но сохраняем актуальные путь и тип
            item.Name = cached.Name;
            item.Year = cached.Year;
            item.Description = cached.Description;
            item.PosterUrl = cached.PosterUrl;
            item.PosterLocalPath = File.Exists(cached.PosterLocalPath ?? string.Empty)
                ? cached.PosterLocalPath
                : null;
            item.KinopoiskId = cached.KinopoiskId;
            item.Rating = cached.Rating;
            item.Genres = cached.Genres ?? new List<string>();

            return true;
        }
        catch
        {
            // Повреждённая запись кэша просто игнорируется
            return false;
        }
    }

    /// <summary>Сохраняет метаданные элемента в кэш.</summary>
    public void Save(MediaItem item)
    {
        try
        {
            var dir = DirFor(item);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(item, JsonOptions));
            Log.Write($"[КЭШ] Сохранено: \"{item.Name}\" -> {dir}");
        }
        catch (Exception ex)
        {
            Log.Write($"[КЭШ] ОШИБКА сохранения \"{item.Name}\": {ex.Message}");
        }
    }

    /// <summary>
    /// Скачивает постер в кэш (если ещё не скачан) и возвращает локальный путь.
    /// </summary>
    public async Task<string?> DownloadPosterAsync(IMetadataProvider provider, MediaItem item, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(item.PosterUrl))
        {
            return null;
        }

        var dir = DirFor(item);
        var posterPath = Path.Combine(dir, "poster.jpg");

        try
        {
            if (!File.Exists(posterPath))
            {
                Directory.CreateDirectory(dir);
                if (provider is KinopoiskProvider kp)
                {
                    await kp.DownloadPosterAsync(item.PosterUrl, posterPath, ct);
                }
                else
                {
                    return null;
                }
            }

            item.PosterLocalPath = posterPath;
            Log.Write($"[КЭШ] Постер скачан: {posterPath}");
            return posterPath;
        }
        catch (Exception ex)
        {
            Log.Write($"[КЭШ] ОШИБКА скачивания постера \"{item.Name}\": {ex.Message}");
            return null;
        }
    }
}
