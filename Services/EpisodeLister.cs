using System.IO;
using System.Text.RegularExpressions;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Построитель списка серий: находит все видеофайлы в папке сериала
/// (включая вложенные папки сезонов) и сортирует их по номеру сезона и эпизода.
/// </summary>
public static partial class EpisodeLister
{
    // Метка сезона/эпизода в имени файла: "S01E05", "s2e10", "S01 E05"
    [GeneratedRegex(@"[sS](\d{1,2})[\s._-]*[eE](\d{1,3})")]
    private static partial Regex SeasonEpisodeRegex();

    /// <summary>
    /// Возвращает список серий сериала, отсортированный по сезону и номеру эпизода
    /// (при отсутствии меток — по имени файла).
    /// </summary>
    public static List<Episode> GetEpisodes(string seriesFolder)
    {
        if (string.IsNullOrWhiteSpace(seriesFolder) || !Directory.Exists(seriesFolder))
        {
            return [];
        }

        var episodes = Directory
            .EnumerateFiles(seriesFolder, "*", SearchOption.AllDirectories)
            .Where(LibraryScanner.IsVideoFile)
            .Select(ParseEpisode)
            .OrderBy(e => e.Season ?? int.MaxValue)
            .ThenBy(e => e.Number ?? int.MaxValue)
            .ThenBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Log.Write($"[СЕРИИ] \"{seriesFolder}\": найдено {episodes.Count} эпизод(ов).");
        return episodes;
    }

    /// <summary>Разбирает имя файла эпизода: название и метку сезона/эпизода.</summary>
    private static Episode ParseEpisode(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var (name, _) = TitleParser.Parse(fileName);

        int? season = null;
        int? number = null;
        var match = SeasonEpisodeRegex().Match(fileName);
        if (match.Success)
        {
            season = int.Parse(match.Groups[1].Value);
            number = int.Parse(match.Groups[2].Value);

            // Убираем метку из названия, чтобы не дублировалась в подписи
            name = name.Replace(match.Value, " ").Trim();
            name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return new Episode
        {
            Path = filePath,
            FileName = fileName,
            Name = string.IsNullOrWhiteSpace(name) ? fileName : name,
            Season = season,
            Number = number
        };
    }
}
