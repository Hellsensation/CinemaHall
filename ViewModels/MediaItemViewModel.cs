using System.Collections.ObjectModel;
using CinemaHall.Models;
using CinemaHall.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CinemaHall.ViewModels;

/// <summary>
/// ViewModel элемента каталога: плитка афиши и панель описания.
/// </summary>
public partial class MediaItemViewModel : ObservableObject
{
    private readonly WatchHistoryService _history;

    /// <summary>Доменная модель.</summary>
    public MediaItem Item { get; }

    public MediaItemViewModel(MediaItem item, WatchHistoryService history)
    {
        Item = item;
        _history = history;

        // При изменении списка серий обновляем видимость секции «Серии»
        Episodes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasEpisodes));
            RefreshWatchSummary();
        };

        LoadEpisodes();
    }

    /// <summary>Серии сериала (пусто для фильмов).</summary>
    public ObservableCollection<EpisodeViewModel> Episodes { get; } = new();

    /// <summary>Есть ли серии для отображения (управляет видимостью секции).</summary>
    public bool HasEpisodes => Episodes.Count > 0;

    /// <summary>Сколько серий всего.</summary>
    public int EpisodesCount => Episodes.Count;

    /// <summary>Сколько серий просмотрено.</summary>
    public int WatchedCount => Episodes.Count(e => e.IsWatched);

    /// <summary>Есть ли хоть одна просмотренная серия (для плитки).</summary>
    public bool HasWatchProgress => WatchedCount > 0;

    /// <summary>Доля просмотренных серий: 0..1.</summary>
    public double WatchProgress =>
        EpisodesCount == 0 ? 0 : (double)WatchedCount / EpisodesCount;

    /// <summary>Подпись под плиткой: «Просмотрено 3 из 10».</summary>
    public string WatchSummaryLabel => $"Просмотрено {WatchedCount} из {EpisodesCount}";

    /// <summary>Заголовок секции серий: «Серии · просмотрено 3 из 10».</summary>
    public string EpisodesHeader =>
        EpisodesCount == 0 ? "Серии" : $"Серии · просмотрено {WatchedCount} из {EpisodesCount}";

    /// <summary>Первая непросмотренная серия (для продолжения просмотра).</summary>
    public EpisodeViewModel? NextEpisode => Episodes.FirstOrDefault(e => !e.IsWatched);

    /// <summary>Отображаемое название с годом: «Начало (2010)».</summary>
    public string DisplayName =>
        Item.Name + (Item.Year is int year ? $" ({year})" : string.Empty);

    /// <summary>Тип для подписи под плиткой.</summary>
    public string KindLabel => Item.Kind == MediaKind.Series ? "Сериал" : "Фильм";

    /// <summary>Рейтинг Кинопоиска.</summary>
    public string RatingLabel =>
        Item.Rating is double rating && rating > 0 ? $"★ {rating:0.0}" : string.Empty;

    /// <summary>Жанры через запятую.</summary>
    public string GenresLabel => string.Join(", ", Item.Genres);

    /// <summary>Описание (или заглушка).</summary>
    public string Description =>
        string.IsNullOrWhiteSpace(Item.Description) ? "Описание недоступно." : Item.Description;

    /// <summary>Локальный путь к постеру.</summary>
    public string? PosterLocalPath => Item.PosterLocalPath;

    /// <summary>Пересобирает список серий из папки сериала и накладывает историю просмотров.</summary>
    public void LoadEpisodes()
    {
        Episodes.Clear();

        if (Item.Kind == MediaKind.Series)
        {
            foreach (var episode in EpisodeLister.GetEpisodes(Item.Path))
            {
                Episodes.Add(new EpisodeViewModel(episode, _history.GetState(Item.Path, episode)));
            }
        }

        MarkNextEpisode();
        RefreshWatchSummary();
    }

    /// <summary>Помечает первую непросмотренную серию как «следующая к просмотру».</summary>
    public void MarkNextEpisode()
    {
        var next = Episodes.FirstOrDefault(e => !e.IsWatched);
        foreach (var episode in Episodes)
        {
            episode.IsNext = ReferenceEquals(episode, next);
        }
    }

    /// <summary>Пересчитывает сводку просмотра (после смены отметок).</summary>
    public void RefreshWatchSummary()
    {
        // «Следующая к просмотру» могла сместиться на другую серию
        MarkNextEpisode();

        OnPropertyChanged(nameof(EpisodesCount));
        OnPropertyChanged(nameof(WatchedCount));
        OnPropertyChanged(nameof(HasWatchProgress));
        OnPropertyChanged(nameof(WatchProgress));
        OnPropertyChanged(nameof(WatchSummaryLabel));
        OnPropertyChanged(nameof(EpisodesHeader));
        OnPropertyChanged(nameof(NextEpisode));
    }

    /// <summary>Обновляет все привязки после дозагрузки метаданных или постера.</summary>
    public void Refresh()
    {
        // Состав папки мог измениться (FileSystemWatcher) — пересканируем серии
        LoadEpisodes();

        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(RatingLabel));
        OnPropertyChanged(nameof(GenresLabel));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(PosterLocalPath));
    }
}
