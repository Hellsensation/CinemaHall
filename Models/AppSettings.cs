namespace CinemaHall.Models;

/// <summary>
/// Настройки приложения: папки каталога и API-ключ Кинопоиска.
/// </summary>
public class AppSettings
{
    /// <summary>Папка с фильмами (видеофайлы в корне).</summary>
    public string? MoviesFolder { get; set; }

    /// <summary>Папка с сериалами (каждая подпапка — отдельный сериал).</summary>
    public string? SeriesFolder { get; set; }

    /// <summary>API-ключ сервиса Kinopoisk API Unofficial.</summary>
    public string? KinopoiskApiKey { get; set; }

    /// <summary>Как отмечать серии просмотренными.</summary>
    public AutoMarkMode AutoMarkMode { get; set; } = AutoMarkMode.AfterThreshold;

    /// <summary>
    /// Через сколько минут после запуска серии ставить отметку «просмотрено»
    /// (используется в режиме <see cref="Models.AutoMarkMode.AfterThreshold"/>).
    /// </summary>
    public int AutoMarkThresholdMinutes { get; set; } = 10;
}
