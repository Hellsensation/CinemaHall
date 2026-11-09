using CinemaHall.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaHall.ViewModels;

/// <summary>
/// ViewModel окна настроек: папки каталога и API-ключ.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _moviesFolder = string.Empty;

    [ObservableProperty]
    private string _seriesFolder = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string? _error;

    /// <summary>Устанавливается в true при успешном сохранении.</summary>
    public bool Saved { get; private set; }

    /// <summary>Окно настроек просит закрыть себя.</summary>
    public event EventHandler? CloseRequested;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        var settings = _settingsService.Load();
        _moviesFolder = settings.MoviesFolder ?? string.Empty;
        _seriesFolder = settings.SeriesFolder ?? string.Empty;
        _apiKey = settings.KinopoiskApiKey ?? string.Empty;
    }

    /// <summary>Сохраняет настройки после валидации.</summary>
    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Error = "Введите API-ключ (получите его на kinopoiskapiunofficial.tech).";
            return;
        }

        if (string.IsNullOrWhiteSpace(MoviesFolder) && string.IsNullOrWhiteSpace(SeriesFolder))
        {
            Error = "Укажите хотя бы одну папку: с фильмами или с сериалами.";
            return;
        }

        _settingsService.Save(new Models.AppSettings
        {
            MoviesFolder = string.IsNullOrWhiteSpace(MoviesFolder) ? null : MoviesFolder.Trim(),
            SeriesFolder = string.IsNullOrWhiteSpace(SeriesFolder) ? null : SeriesFolder.Trim(),
            KinopoiskApiKey = ApiKey.Trim()
        });

        Saved = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
