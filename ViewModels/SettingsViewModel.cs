using CinemaHall.Models;
using CinemaHall.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaHall.ViewModels;

/// <summary>
/// ViewModel окна настроек: папки каталога, API-ключ и правила отметки просмотра.
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

    /// <summary>Выбранный способ отметки просмотра.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualMode))]
    [NotifyPropertyChangedFor(nameof(IsOnLaunchMode))]
    [NotifyPropertyChangedFor(nameof(IsAfterThresholdMode))]
    private AutoMarkMode _markMode = AutoMarkMode.AfterThreshold;

    /// <summary>Порог автопометки в минутах (вводится как текст для валидации).</summary>
    [ObservableProperty]
    private string _thresholdMinutesText = "10";

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
        _markMode = settings.AutoMarkMode;
        _thresholdMinutesText = (settings.AutoMarkThresholdMinutes is >= 1 and <= 180
            ? settings.AutoMarkThresholdMinutes
            : 10).ToString();
    }

    /// <summary>Отметка ставится только вручную.</summary>
    public bool IsManualMode
    {
        get => MarkMode == AutoMarkMode.Off;
        set
        {
            if (value)
            {
                MarkMode = AutoMarkMode.Off;
            }
        }
    }

    /// <summary>Отметка ставится сразу при запуске серии.</summary>
    public bool IsOnLaunchMode
    {
        get => MarkMode == AutoMarkMode.OnLaunch;
        set
        {
            if (value)
            {
                MarkMode = AutoMarkMode.OnLaunch;
            }
        }
    }

    /// <summary>Отметка ставится через заданное число минут.</summary>
    public bool IsAfterThresholdMode
    {
        get => MarkMode == AutoMarkMode.AfterThreshold;
        set
        {
            if (value)
            {
                MarkMode = AutoMarkMode.AfterThreshold;
            }
        }
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

        var threshold = 10;
        if (MarkMode == AutoMarkMode.AfterThreshold)
        {
            if (!int.TryParse(ThresholdMinutesText.Trim(), out threshold) ||
                threshold is < 1 or > 180)
            {
                Error = "Порог автопометки — целое число минут от 1 до 180.";
                return;
            }
        }
        else if (int.TryParse(ThresholdMinutesText.Trim(), out var parsed))
        {
            // Сохраняем последнее корректное значение, чтобы не потерять его при смене режима
            threshold = Math.Clamp(parsed, 1, 180);
        }

        _settingsService.Save(new Models.AppSettings
        {
            MoviesFolder = string.IsNullOrWhiteSpace(MoviesFolder) ? null : MoviesFolder.Trim(),
            SeriesFolder = string.IsNullOrWhiteSpace(SeriesFolder) ? null : SeriesFolder.Trim(),
            KinopoiskApiKey = ApiKey.Trim(),
            AutoMarkMode = MarkMode,
            AutoMarkThresholdMinutes = threshold
        });

        Saved = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
