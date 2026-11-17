using CinemaHall.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CinemaHall.ViewModels;

/// <summary>
/// ViewModel серии сериала: строка в списке серий на панели описания.
/// </summary>
public partial class EpisodeViewModel : ObservableObject
{
    /// <summary>Доменная модель серии.</summary>
    public Episode Episode { get; }

    /// <summary>Состояние просмотра: не просмотрена / в процессе / просмотрена.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWatched))]
    [NotifyPropertyChangedFor(nameof(IsInProgress))]
    [NotifyPropertyChangedFor(nameof(WatchedGlyph))]
    [NotifyPropertyChangedFor(nameof(WatchedTooltip))]
    private WatchState _state;

    /// <summary>Серия — следующая к просмотру (первая непросмотренная).</summary>
    [ObservableProperty]
    private bool _isNext;

    public EpisodeViewModel(Episode episode, WatchState state)
    {
        Episode = episode;
        _state = state;
    }

    /// <summary>Подпись серии: «S01E05 · Название» или просто название.</summary>
    public string DisplayName =>
        Episode.Season is int season && Episode.Number is int number
            ? $"S{season:00}E{number:00} · {Episode.Name}"
            : Episode.Name;

    /// <summary>Короткая метка серии: «S01E05» (пусто, если номер не распознан).</summary>
    public string ShortLabel =>
        Episode.Season is int season && Episode.Number is int number
            ? $"S{season:00}E{number:00}"
            : string.Empty;

    /// <summary>Полный путь к файлу серии (для всплывающей подсказки).</summary>
    public string FilePath => Episode.Path;

    /// <summary>Просмотрена ли серия.</summary>
    public bool IsWatched => State == WatchState.Watched;

    /// <summary>Просмотр начат, но отметка ещё не поставлена.</summary>
    public bool IsInProgress => State == WatchState.InProgress;

    /// <summary>Значок состояния просмотра для кнопки-переключателя.</summary>
    public string WatchedGlyph => State switch
    {
        WatchState.Watched => "✓",
        WatchState.InProgress => "…",
        _ => "○"
    };

    /// <summary>Подсказка к значку состояния.</summary>
    public string WatchedTooltip => State switch
    {
        WatchState.Watched => "Просмотрено — нажмите, чтобы снять отметку",
        WatchState.InProgress => "Просмотр начат — отметка появится автоматически",
        _ => "Не просмотрено — нажмите, чтобы отметить"
    };

    /// <summary>Синхронизирует привязки после пересканирования серий.</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
