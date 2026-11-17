using System.IO;
using System.Text.Json;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// История просмотров серий: хранится в %LocalAppData%\CinemaHall\watch-history.json.
/// Ключ сериала — имя его папки, ключ серии — «S01E05» или относительный путь файла,
/// поэтому перенос библиотеки и переименование файлов не сбрасывают отметки.
/// </summary>
public class WatchHistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CinemaHall",
        "watch-history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private Dictionary<string, SeriesProgress>? _data;

    /// <summary>Ключ сериала: имя его папки в нижнем регистре.</summary>
    public static string SeriesKey(string seriesFolder)
    {
        var trimmed = seriesFolder.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);

        return string.IsNullOrWhiteSpace(name)
            ? trimmed.ToLowerInvariant()
            : name.ToLowerInvariant();
    }

    /// <summary>
    /// Ключ серии: «S01E05», если номер распознан, иначе путь файла
    /// относительно папки сериала.
    /// </summary>
    public static string EpisodeKey(Episode episode, string seriesFolder)
    {
        if (episode.Season is int season && episode.Number is int number)
        {
            return $"S{season:00}E{number:00}";
        }

        return Path.GetRelativePath(seriesFolder, episode.Path)
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    /// <summary>Возвращает состояние серии (по умолчанию — не просмотрена).</summary>
    public WatchState GetState(string seriesFolder, Episode episode)
    {
        lock (_gate)
        {
            return Data.TryGetValue(SeriesKey(seriesFolder), out var series) &&
                   series.Episodes.TryGetValue(EpisodeKey(episode, seriesFolder), out var progress)
                ? progress.State
                : WatchState.None;
        }
    }

    /// <summary>Устанавливает состояние серии и сохраняет историю на диск.</summary>
    public void SetState(string seriesFolder, Episode episode, WatchState state)
    {
        lock (_gate)
        {
            var seriesKey = SeriesKey(seriesFolder);
            if (!Data.TryGetValue(seriesKey, out var series))
            {
                series = new SeriesProgress();
                Data[seriesKey] = series;
            }

            var episodeKey = EpisodeKey(episode, seriesFolder);

            // Отметку сняли — запись не нужна, файл истории не разрастается
            if (state == WatchState.None)
            {
                if (series.Episodes.Remove(episodeKey))
                {
                    Save();
                }

                return;
            }

            if (!series.Episodes.TryGetValue(episodeKey, out var progress))
            {
                progress = new EpisodeProgress();
                series.Episodes[episodeKey] = progress;
            }

            progress.State = state;
            progress.UpdatedAt = DateTimeOffset.Now;
            if (state == WatchState.InProgress)
            {
                progress.StartedAt = DateTimeOffset.Now;
            }

            Save();
        }
    }

    /// <summary>
    /// Досчитывает серии, просмотр которых начался до закрытия приложения:
    /// если с запуска прошло больше порога, они считаются просмотренными.
    /// </summary>
    public int RecoverPending(TimeSpan threshold)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.Now;
            var recovered = 0;

            foreach (var series in Data.Values)
            {
                foreach (var progress in series.Episodes.Values)
                {
                    if (progress.State == WatchState.InProgress &&
                        now - progress.StartedAt >= threshold)
                    {
                        progress.State = WatchState.Watched;
                        progress.UpdatedAt = now;
                        recovered++;
                    }
                }
            }

            if (recovered > 0)
            {
                Save();
            }

            return recovered;
        }
    }

    private Dictionary<string, SeriesProgress> Data => _data ??= Load();

    private static Dictionary<string, SeriesProgress> Load()
    {
        // Сравнение без учёта регистра: имена папок в Windows регистронезависимы
        var result = new Dictionary<string, SeriesProgress>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(HistoryPath))
            {
                return result;
            }

            var json = File.ReadAllText(HistoryPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, SeriesProgress>>(json, JsonOptions);
            if (loaded is null)
            {
                return result;
            }

            // Пересобираем словари: после десериализации теряется компаратор
            foreach (var (seriesKey, series) in loaded)
            {
                var copy = new SeriesProgress();
                foreach (var (episodeKey, progress) in series.Episodes)
                {
                    copy.Episodes[episodeKey] = progress;
                }

                result[seriesKey] = copy;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[ПРОСМОТРЫ] Не удалось прочитать историю: {ex.Message}");
        }

        return result;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            var json = JsonSerializer.Serialize(Data, JsonOptions);

            // Пишем через временный файл, чтобы не оставить повреждённый JSON
            var temp = HistoryPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, HistoryPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Write($"[ПРОСМОТРЫ] Не удалось сохранить историю: {ex.Message}");
        }
    }
}
