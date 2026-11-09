namespace CinemaHall.Models;

/// <summary>
/// Тип медиа-элемента каталога.
/// </summary>
public enum MediaKind
{
    /// <summary>Фильм (отдельный видеофайл).</summary>
    Movie,
    /// <summary>Сериал (папка с эпизодами).</summary>
    Series
}

/// <summary>
/// Доменная модель элемента каталога: фильм или сериал.
/// </summary>
public class MediaItem
{
    /// <summary>Название тайтла (из имени файла или папки).</summary>
    public required string Name { get; set; }

    /// <summary>Год выпуска (если удалось распознать из имени).</summary>
    public int? Year { get; set; }

    /// <summary>Тип: фильм или сериал.</summary>
    public required MediaKind Kind { get; set; }

    /// <summary>Путь к видеофайлу (фильм) или к папке сериала.</summary>
    public required string Path { get; set; }

    /// <summary>Описание (сюжет) из Кинопоиска.</summary>
    public string? Description { get; set; }

    /// <summary>URL постера из Кинопоиска.</summary>
    public string? PosterUrl { get; set; }

    /// <summary>Локальный путь к скачанному постеру в кэше.</summary>
    public string? PosterLocalPath { get; set; }

    /// <summary>Идентификатор на Кинопоиске.</summary>
    public int? KinopoiskId { get; set; }

    /// <summary>Рейтинг Кинопоиска.</summary>
    public double? Rating { get; set; }

    /// <summary>Жанры.</summary>
    public List<string> Genres { get; set; } = new();
}
