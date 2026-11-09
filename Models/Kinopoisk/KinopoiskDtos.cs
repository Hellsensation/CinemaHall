using System.Text.Json.Serialization;

namespace CinemaHall.Models.Kinopoisk;

/// <summary>
/// DTO ответа поиска: /api/v2.1/films/search-by-keyword.
/// </summary>
public class SearchByKeywordResponse
{
    public List<SearchFilmDto> Films { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// Краткая информация о тайтле из результатов поиска.
/// </summary>
public class SearchFilmDto
{
    public int FilmId { get; set; }
    public string? NameRu { get; set; }
    public string? NameEn { get; set; }
    public string? NameOriginal { get; set; }
    public string? Year { get; set; }

    /// <summary>Тип тайтла: FILM, TV_SERIES, TV_SHOW и т.д. (в API поле называется "type").</summary>
    [JsonPropertyName("type")]
    public string? FilmType { get; set; }

    public string? PosterUrlPreview { get; set; }
    public string? Description { get; set; }
    public string? Rating { get; set; }
    public List<GenreDto> Genres { get; set; } = new();
}

/// <summary>
/// DTO детальной информации: /api/v2.2/films/{id}.
/// </summary>
public class FilmDetailsDto
{
    public int KinopoiskId { get; set; }
    public string? NameRu { get; set; }
    public string? NameOriginal { get; set; }

    /// <summary>Год: в эндпоинте деталей приходит числом, в поиске — строкой.</summary>
    public int? Year { get; set; }

    public string? PosterUrl { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? Slogan { get; set; }
    public double? RatingKinopoisk { get; set; }
    public bool Serial { get; set; }
    public List<GenreDto> Genres { get; set; } = new();
}

/// <summary>
/// DTO жанра.
/// </summary>
public class GenreDto
{
    public string? Genre { get; set; }
}
