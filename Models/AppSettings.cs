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
}
