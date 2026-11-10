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

    public EpisodeViewModel(Episode episode)
    {
        Episode = episode;
    }

    /// <summary>Подпись серии: «S01E05 · Название» или просто название.</summary>
    public string DisplayName =>
        Episode.Season is int season && Episode.Number is int number
            ? $"S{season:00}E{number:00} · {Episode.Name}"
            : Episode.Name;

    /// <summary>Полный путь к файлу серии (для всплывающей подсказки).</summary>
    public string FilePath => Episode.Path;

    /// <summary>Синхронизирует привязки после пересканирования серий.</summary>
    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
