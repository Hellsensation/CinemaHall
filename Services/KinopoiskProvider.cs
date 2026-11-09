using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CinemaHall.Models;
using CinemaHall.Models.Kinopoisk;

namespace CinemaHall.Services;

/// <summary>
/// Провайдер метаданных на базе Kinopoisk API Unofficial (kinopoiskapiunofficial.tech).
/// Бесплатный тариф: 200 запросов в сутки, не более 5 запросов в секунду —
/// поэтому реализовано ограничение частоты запросов.
/// </summary>
public class KinopoiskProvider : IMetadataProvider
{
    private const string BaseUrl = "https://kinopoiskapiunofficial.tech";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    // Ограничение частоты: не чаще одного запроса в 250 мс (5 в секунду)
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public KinopoiskProvider(string apiKey)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Ищет тайтл через search-by-keyword, выбирает лучший вариант и
    /// дозаполняет данные из детального эндпоинта.
    /// </summary>
    public async Task<bool> TryFillAsync(MediaItem item, CancellationToken ct = default)
    {
        // Формируем поисковое выражение: "название год"
        var expression = item.Year is null ? item.Name : $"{item.Name} {item.Year}";
        var url = $"{BaseUrl}/api/v2.1/films/search-by-keyword?keyword={Uri.EscapeDataString(expression)}&page=1";

        var search = await SendAsync<SearchByKeywordResponse>(url, ct);
        if (search?.Films is not { Count: > 0 })
        {
            // Вторая попытка без года (год в имени файла иногда бывает не годом выпуска)
            if (item.Year is null)
            {
                return false;
            }

            url = $"{BaseUrl}/api/v2.1/films/search-by-keyword?keyword={Uri.EscapeDataString(item.Name)}&page=1";
            search = await SendAsync<SearchByKeywordResponse>(url, ct);
            if (search?.Films is not { Count: > 0 })
            {
                return false;
            }
        }

        // Выбираем наиболее подходящий результат
        var best = PickBest(search.Films, item.Name, item.Year, item.Kind);
        if (best is null)
        {
            return false;
        }

        // Заполняем данные из результатов поиска
        ApplySearchResult(item, best);

        // Детальная информация: полное описание, постер, рейтинг
        var details = await SendAsync<FilmDetailsDto>($"{BaseUrl}/api/v2.2/films/{best.FilmId}", ct);
        if (details != null)
        {
            if (!string.IsNullOrWhiteSpace(details.Description))
            {
                item.Description = details.Description;
            }
            else if (string.IsNullOrWhiteSpace(item.Description) && !string.IsNullOrWhiteSpace(details.ShortDescription))
            {
                item.Description = details.ShortDescription;
            }

            item.PosterUrl ??= details.PosterUrl;
            item.KinopoiskId = details.KinopoiskId;
            item.Rating = details.RatingKinopoisk;

            item.Genres = details.Genres
                .Where(g => !string.IsNullOrWhiteSpace(g.Genre))
                .Select(g => g.Genre!)
                .ToList();
        }

        return true;
    }

    /// <summary>
    /// Скачивает постер по URL и сохраняет его по указанному пути.
    /// </summary>
    public async Task DownloadPosterAsync(string posterUrl, string targetPath, CancellationToken ct = default)
    {
        await ThrottleAsync(ct);
        using var response = await _http.GetAsync(posterUrl, ct);
        response.EnsureSuccessStatusCode();
        await using var fs = File.Create(targetPath);
        await response.Content.CopyToAsync(fs, ct);
    }

    /// <summary>
    /// Выбирает лучший результат поиска: точное совпадение названия и года имеет приоритет.
    /// </summary>
    private static SearchFilmDto? PickBest(List<SearchFilmDto> films, string name, int? year, MediaKind kind)
    {
        var wantedKind = kind == MediaKind.Series ? "TV_SERIES" : "FILM";
        var normalized = Normalize(name);

        // 1) Совпадение названия + года + типа
        var best = films.FirstOrDefault(f =>
            MatchesKind(f, wantedKind) &&
            (Normalize(f.NameRu) == normalized || Normalize(f.NameEn) == normalized || Normalize(f.NameOriginal) == normalized) &&
            (year is null || f.Year?.StartsWith(year.Value.ToString()) == true));

        // 2) Совпадение названия + года
        best ??= films.FirstOrDefault(f =>
            (Normalize(f.NameRu) == normalized || Normalize(f.NameEn) == normalized || Normalize(f.NameOriginal) == normalized) &&
            (year is null || f.Year?.StartsWith(year.Value.ToString()) == true));

        // 3) Совпадение названия
        best ??= films.FirstOrDefault(f =>
            Normalize(f.NameRu) == normalized || Normalize(f.NameEn) == normalized || Normalize(f.NameOriginal) == normalized);

        // 4) Первое совпадение по типу
        best ??= films.FirstOrDefault(f => MatchesKind(f, wantedKind));

        // 5) Иначе просто первый результат
        return best ?? films.FirstOrDefault();
    }

    private static bool MatchesKind(SearchFilmDto film, string wantedKind) =>
        string.Equals(film.FilmType, wantedKind, StringComparison.OrdinalIgnoreCase);

    private static void ApplySearchResult(MediaItem item, SearchFilmDto film)
    {
        item.KinopoiskId = film.FilmId;
        item.PosterUrl ??= film.PosterUrlPreview;

        if (item.Rating is null && double.TryParse(film.Rating, out var rating))
        {
            item.Rating = rating;
        }

        if (string.IsNullOrWhiteSpace(item.Description) && !string.IsNullOrWhiteSpace(film.Description))
        {
            item.Description = film.Description;
        }

        if (item.Genres.Count == 0)
        {
            item.Genres = film.Genres
                .Where(g => !string.IsNullOrWhiteSpace(g.Genre))
                .Select(g => g.Genre!)
                .ToList();
        }
    }

    /// <summary>Нормализация строки для сравнения: нижний регистр, без пунктуации.</summary>
    private static string Normalize(string? text) =>
        string.Concat((text ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>Ограничивает частоту запросов к API.</summary>
    private async Task ThrottleAsync(CancellationToken ct)
    {
        await _rateGate.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            var delay = TimeSpan.FromMilliseconds(250) - elapsed;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }
            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            _rateGate.Release();
        }
    }

    /// <summary>Выполняет GET-запрос и десериализует JSON-ответ, обрабатывая ошибки API.</summary>
    private async Task<T?> SendAsync<T>(string url, CancellationToken ct)
    {
        var response = await GetWithRetryAsync(url, ct);
        try
        {
            // 401/402 — неверный или исчерпанный API-ключ
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                throw new InvalidOperationException(
                    "Проблема с API-ключом Кинопоиска: ключ неверный или лимит запросов исчерпан (200 запросов/сутки на бесплатном тарифе).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Выполняет GET-запрос с повтором при превышении лимита (429):
    /// ждём 2 секунды и пробуем ещё раз один раз.
    /// </summary>
    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken ct)
    {
        await ThrottleAsync(ct);
        var response = await _http.GetAsync(url, ct);

        // 429 — превышение лимита запросов: ждём и повторяем один раз
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            await ThrottleAsync(ct);
            response = await _http.GetAsync(url, ct);
        }

        return response;
    }
}
