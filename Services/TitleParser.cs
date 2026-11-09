using System.IO;
using System.Text.RegularExpressions;

namespace CinemaHall.Services;

/// <summary>
/// Извлекает название и год из имени файла или папки, например:
/// "Начало.2010.BDRip.1080p" -> ("Начало", 2010),
/// "The Leftovers 2014 S01" -> ("The Leftovers S01" -> "The Leftovers", 2014).
/// </summary>
public static partial class TitleParser
{
    // Год: 4 цифры 1900–2099, окружённые не-цифрами
    [GeneratedRegex(@"(?<!\d)(19\d{2}|20\d{2})(?!\d)")]
    private static partial Regex YearRegex();

    // Служебные слова, характерные для рипов (качество, кодеки, релиз-группы)
    private static readonly HashSet<string> NoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "BDRip", "HDRip", "WEB-DL", "WEBDL", "WEBRip", "BluRay", "BRRip", "DVDRip",
        "1080p", "720p", "2160p", "480p", "4K", "UHD",
        "x264", "x265", "h264", "h265", "HEVC", "AVC", "HDTV", "SATRip", "TVRip",
        "DVDScr", "TS", "TC", "CAM", "AC3", "DTS", "AAC", "MP3",
        "LostFilm", "HDRezka", "Kinoshow", "Extended", "Unrated", "Directors", "Cut",
        "MVO", "DUB", "DVO", "APO", "VO", "Dual", "ITunes", "Netflix", "AMZN", "NF"
    };

    /// <summary>
    /// Разбирает имя файла или папки, возвращая очищенное название и год (если найден).
    /// </summary>
    public static (string Name, int? Year) Parse(string fileNameOrFolderName)
    {
        // Убираем расширение, если передано имя файла
        var raw = Path.HasExtension(fileNameOrFolderName)
            ? Path.GetFileNameWithoutExtension(fileNameOrFolderName)
            : fileNameOrFolderName.Trim();

        var year = (int?)null;

        var match = YearRegex().Match(raw);
        if (match.Success)
        {
            var candidate = int.Parse(match.Groups[1].Value);
            // Год валиден, если это не выглядит как часть другого числа/кода — 1900..2099 гарантирован регуляркой
            year = candidate;
            // Название — всё, что до года
            raw = raw[..match.Index];
        }

        // Заменяем разделители на пробелы и убираем скобки
        var cleaned = raw.Replace('.', ' ').Replace('_', ' ')
                         .Replace('(', ' ').Replace(')', ' ')
                         .Replace('[', ' ').Replace(']', ' ')
                         .Replace('-', ' ');

        // Убираем служебные теги качества и т.п.
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !NoiseTags.Contains(w) && !NoiseTags.Contains(w.ToUpperInvariant()));

        var name = string.Join(' ', words).Trim();

        // Если после очистки ничего не осталось — возвращаем исходное имя без расширения
        return string.IsNullOrWhiteSpace(name)
            ? (raw.Trim(), year)
            : (name, year);
    }
}
