using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CinemaHall.Models;
using CinemaHall.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CinemaHall.ViewModels;

/// <summary>
/// ViewModel элемента каталога: плитка афиши и панель описания.
/// </summary>
public partial class MediaItemViewModel : ObservableObject
{
    /// <summary>Доменная модель.</summary>
    public MediaItem Item { get; }

    public MediaItemViewModel(MediaItem item)
    {
        Item = item;

        // При изменении списка серий обновляем видимость секции «Серии»
        Episodes.CollectionChanged += (_, _) =>
            OnPropertyChanged(nameof(HasEpisodes));

        LoadEpisodes();
    }

    /// <summary>Серии сериала (пусто для фильмов).</summary>
    public ObservableCollection<EpisodeViewModel> Episodes { get; } = new();

    /// <summary>Есть ли серии для отображения (управляет видимостью секции).</summary>
    public bool HasEpisodes => Episodes.Count > 0;

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

    /// <summary>Пересобирает список серий из папки сериала.</summary>
    public void LoadEpisodes()
    {
        Episodes.Clear();

        if (Item.Kind != MediaKind.Series)
        {
            return;
        }

        foreach (var episode in EpisodeLister.GetEpisodes(Item.Path))
        {
            Episodes.Add(new EpisodeViewModel(episode));
        }
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
