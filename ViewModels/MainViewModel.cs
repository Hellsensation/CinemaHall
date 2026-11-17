using System.Collections.ObjectModel;
using CinemaHall.Models;
using CinemaHall.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaHall.ViewModels;

/// <summary>
/// Главный ViewModel: коллекция плиток, выбранная афиша, команды.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly MetadataCache _cache = new();
    private readonly LibraryScanner _scanner = new();
    private readonly WatchHistoryService _history = new();
    private readonly PlaybackTracker _tracker;
    private KinopoiskProvider? _provider;
    private CancellationTokenSource? _refreshCts;
    private AutoMarkMode _autoMarkMode = AutoMarkMode.AfterThreshold;
    private int _autoMarkThresholdMinutes = 10;

    [ObservableProperty]
    private ObservableCollection<MediaItemViewModel> _items = new();

    [ObservableProperty]
    private MediaItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _statusText = "Готово";

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel()
    {
        // Автопометка «просмотрено» по таймеру: плеер внешний, поэтому
        // отсчитываем время с момента запуска файла
        _tracker = new PlaybackTracker(_history);
        _tracker.Watched += (_, e) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => OnEpisodeWatched(e));

        // Изменения в библиотеке (FileSystemWatcher) -> повторное построение каталога
        _scanner.LibraryChanged += () =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => _ = RefreshAsync());
    }

    partial void OnSelectedItemChanged(MediaItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
    }

    /// <summary>Выбрана ли афиша (управляет видимостью панели описания).</summary>
    public bool HasSelectedItem => SelectedItem is not null;

    /// <summary>
    /// Инициализация при запуске: построение каталога.
    /// </summary>
    public Task InitializeAsync() => RefreshAsync();

    /// <summary>
    /// Строит каталог: сканирует папки и дозаполняет метаданные (кэш -> Кинопоиск).
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        IsBusy = true;
        StatusText = "Сканирование библиотеки...";

        try
        {
            var settings = _settingsService.Load();

            // Настройки автопометки применяются к новым запускам серий
            _autoMarkMode = settings.AutoMarkMode;
            _autoMarkThresholdMinutes = settings.AutoMarkThresholdMinutes is >= 1 and <= 180
                ? settings.AutoMarkThresholdMinutes
                : 10;

            // Если приложение закрыли, не дождавшись автопометки, досмотренные
            // серии отмечаем при следующем запуске
            if (_autoMarkMode == AutoMarkMode.AfterThreshold)
            {
                var recovered = _history.RecoverPending(TimeSpan.FromMinutes(_autoMarkThresholdMinutes));
                if (recovered > 0)
                {
                    Log.Write($"[ПРОСМОТРЫ] Отмечено просмотренными при восстановлении: {recovered}.");
                }
            }

            // Провайдер создаётся один раз; при смене ключа в настройках будет
            // пересоздан при следующем обновлении
            if (_provider is null && !string.IsNullOrWhiteSpace(settings.KinopoiskApiKey))
            {
                _provider = new KinopoiskProvider(settings.KinopoiskApiKey);
            }

            // Отслеживаем изменения в папках (новые фильмы и сериалы)
            _scanner.StartWatching(settings.MoviesFolder, settings.SeriesFolder);

            // Синхронизируем коллекцию плиток с содержимым папок
            var scanned = _scanner.Scan(settings.MoviesFolder, settings.SeriesFolder);
            Log.Write($"[СКАНИРОВАНИЕ] Найдено {scanned.Count} тайтлов " +
                      $"(фильмы: {settings.MoviesFolder}, сериалы: {settings.SeriesFolder})");
            Items.Clear();
            foreach (var media in scanned)
            {
                Items.Add(new MediaItemViewModel(media, _history));
            }

            StatusText = $"Найдено: {Items.Count}. Загрузка метаданных...";
            await LoadMetadataAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Обновление отменено новым обновлением — ничего не делаем
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Дозаполняет метаданные для всех плиток: сначала кэш, затем API Кинопоиска.
    /// </summary>
    private async Task LoadMetadataAsync(CancellationToken ct)
    {
        if (_provider is null)
        {
            StatusText = $"Найдено: {Items.Count}. API-ключ не задан (откройте настройки).";
            return;
        }

        var total = Items.Count;
        var processed = 0;

        foreach (var tile in Items)
        {
            ct.ThrowIfCancellationRequested();
            processed++;

            // Метаданные уже в кэше — запрос к API не нужен
            if (_cache.TryLoad(tile.Item))
            {
                tile.Refresh();
                continue;
            }

            try
            {
                var filled = await _provider.TryFillAsync(tile.Item, ct);
                if (filled)
                {
                    await _cache.DownloadPosterAsync(_provider, tile.Item, ct);
                    _cache.Save(tile.Item);
                }
                else
                {
                    Log.Write($"[КАТАЛОГ] Метаданные не найдены: \"{tile.Item.Name}\"");
                    tile.Item.Description = "Метаданные не найдены в Кинопоиске.";
                }
            }
            catch (InvalidOperationException ex)
            {
                // Проблема с ключом или лимитом — прекращаем обращения к API
                Log.Write($"[КАТАЛОГ] Прекращение загрузки: {ex.Message}");
                StatusText = ex.Message;
                break;
            }
            catch (Exception ex)
            {
                Log.Write($"[КАТАЛОГ] Ошибка для \"{tile.Item.Name}\": {ex.GetType().Name}: {ex.Message}");
                tile.Item.Description = $"Не удалось получить данные: {ex.Message}";
            }

            tile.Refresh();
            StatusText = $"Загрузка метаданных: {processed} из {total}...";
        }

        if (processed >= total)
        {
            StatusText = $"Готово. В каталоге {total} тайтл(ов).";
        }
    }

    /// <summary>
    /// Клик по серии в списке: запуск эпизода и (по настройке) автопометка просмотра.
    /// </summary>
    [RelayCommand]
    private void PlayEpisode(EpisodeViewModel? episode)
    {
        if (episode is null)
        {
            return;
        }

        var owner = FindOwner(episode);
        if (owner is null)
        {
            return;
        }

        var seriesFolder = owner.Item.Path;

        try
        {
            PlayerLauncher.PlayFile(episode.Episode.Path);
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось запустить серию: {ex.Message}";
            return;
        }

        _tracker.Start(episode.Episode, seriesFolder, _autoMarkMode, _autoMarkThresholdMinutes);

        // Состояние берём из истории: режим «сразу» уже поставил отметку,
        // режим «через N минут» — только начал просмотр
        episode.State = _history.GetState(seriesFolder, episode.Episode);
        owner.RefreshWatchSummary();

        StatusText = _autoMarkMode == AutoMarkMode.AfterThreshold
            ? $"Запущено: {episode.DisplayName}. Отметим просмотренной через {_autoMarkThresholdMinutes} мин."
            : $"Запущено: {episode.DisplayName}";
    }

    /// <summary>
    /// Ручное переключение отметки «просмотрено» у серии.
    /// </summary>
    [RelayCommand]
    private void ToggleWatched(EpisodeViewModel? episode)
    {
        if (episode is null)
        {
            return;
        }

        var owner = FindOwner(episode);
        if (owner is null)
        {
            return;
        }

        // Ручная отметка отменяет отложенную автопометку
        _tracker.Cancel();

        var state = episode.IsWatched ? WatchState.None : WatchState.Watched;
        _history.SetState(owner.Item.Path, episode.Episode, state);
        episode.State = state;
        owner.RefreshWatchSummary();

        StatusText = state == WatchState.Watched
            ? $"Отмечено просмотренным: {episode.DisplayName}"
            : $"Снята отметка о просмотре: {episode.DisplayName}";
    }

    /// <summary>Кнопка «Смотреть»: для сериала продолжает с первой непросмотренной серии.</summary>
    [RelayCommand]
    private void Play()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (SelectedItem.NextEpisode is { } next)
        {
            PlayEpisode(next);
            return;
        }

        try
        {
            PlayerLauncher.Play(SelectedItem.Item);
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось запустить: {ex.Message}";
        }
    }

    /// <summary>Находит плитку, которой принадлежит серия.</summary>
    private MediaItemViewModel? FindOwner(EpisodeViewModel episode) =>
        Items.FirstOrDefault(item => item.Episodes.Contains(episode)) ?? SelectedItem;

    /// <summary>Таймер сообщил, что серия досмотрена: обновляем интерфейс.</summary>
    private void OnEpisodeWatched(EpisodeWatchedEventArgs e)
    {
        var owner = Items.FirstOrDefault(item =>
            string.Equals(item.Item.Path, e.SeriesFolder, StringComparison.OrdinalIgnoreCase));
        var episode = owner?.Episodes.FirstOrDefault(vm =>
            string.Equals(vm.Episode.Path, e.EpisodePath, StringComparison.OrdinalIgnoreCase));

        if (owner is null || episode is null)
        {
            return;
        }

        episode.State = WatchState.Watched;
        owner.RefreshWatchSummary();
        StatusText = $"Серия «{episode.DisplayName}» отмечена просмотренной.";
    }

    /// <summary>Кнопка «Настройки» (окно открывает View).</summary>
    public event EventHandler? SettingsRequested;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _scanner.Dispose();
        _refreshCts?.Dispose();
        _tracker.Dispose();
    }
}
