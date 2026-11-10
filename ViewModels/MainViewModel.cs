using System.Collections.ObjectModel;
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
    private KinopoiskProvider? _provider;
    private CancellationTokenSource? _refreshCts;

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
                Items.Add(new MediaItemViewModel(media));
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
    /// Клик по серии в списке: запуск выбранного эпизода через стандартный плеер.
    /// </summary>
    [RelayCommand]
    private void PlayEpisode(EpisodeViewModel? episode)
    {
        if (episode is not null)
        {
            PlayerLauncher.PlayFile(episode.Episode.Path);
        }
    }

    /// <summary>Кнопка «Смотреть»: запуск воспроизведения через стандартный плеер.</summary>
    [RelayCommand]
    private void Play()
    {
        if (SelectedItem is not null)
        {
            PlayerLauncher.Play(SelectedItem.Item);
        }
    }

    /// <summary>Кнопка «Настройки» (окно открывает View).</summary>
    public event EventHandler? SettingsRequested;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _scanner.Dispose();
        _refreshCts?.Dispose();
    }
}
