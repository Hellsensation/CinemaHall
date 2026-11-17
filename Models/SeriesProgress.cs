namespace CinemaHall.Models;

/// <summary>
/// История просмотра сериала: состояние каждой серии по её ключу.
/// </summary>
public class SeriesProgress
{
    /// <summary>
    /// Прогресс по сериям. Ключ — «S01E05», если номер распознан,
    /// иначе путь файла относительно папки сериала.
    /// </summary>
    public Dictionary<string, EpisodeProgress> Episodes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
