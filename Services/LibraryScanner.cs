using System.IO;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Сканер библиотеки: находит фильмы (видеофайлы в корне папки фильмов)
/// и сериалы (подпапки папки сериалов), отслеживает изменения через FileSystemWatcher.
/// </summary>
public class LibraryScanner : IDisposable
{
    /// <summary>Поддерживаемые видеоформаты.</summary>
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".mpg", ".mpeg", ".ts"
    };

    private FileSystemWatcher? _moviesWatcher;
    private FileSystemWatcher? _seriesWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly SemaphoreSlim _debounceLock = new(1, 1);

    /// <summary>Событие возникает после изменений в библиотеке (с защитой от «дребезга» событий).</summary>
    public event Action? LibraryChanged;

    /// <summary>Проверяет, является ли файл видеофайлом.</summary>
    public static bool IsVideoFile(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Сканирует папки фильмов и сериалов, возвращает список элементов каталога.
    /// </summary>
    public List<MediaItem> Scan(string? moviesFolder, string? seriesFolder)
    {
        var result = new List<MediaItem>();

        // Фильмы: видеофайлы в корне папки фильмов
        if (!string.IsNullOrWhiteSpace(moviesFolder) && Directory.Exists(moviesFolder))
        {
            foreach (var file in Directory.EnumerateFiles(moviesFolder).Where(IsVideoFile))
            {
                var (name, year) = TitleParser.Parse(Path.GetFileName(file));
                result.Add(new MediaItem
                {
                    Name = name,
                    Year = year,
                    Kind = MediaKind.Movie,
                    Path = file
                });
            }
        }

        // Сериалы: каждая подпапка — отдельный сериал с общим описанием и постером
        if (!string.IsNullOrWhiteSpace(seriesFolder) && Directory.Exists(seriesFolder))
        {
            foreach (var dir in Directory.EnumerateDirectories(seriesFolder))
            {
                var (name, year) = TitleParser.Parse(Path.GetFileName(dir));
                result.Add(new MediaItem
                {
                    Name = name,
                    Year = year,
                    Kind = MediaKind.Series,
                    Path = dir
                });
            }
        }

        return result.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>
    /// Запускает отслеживание изменений в папках фильмов и сериалов.
    /// </summary>
    public void StartWatching(string? moviesFolder, string? seriesFolder)
    {
        StopWatching();

        if (!string.IsNullOrWhiteSpace(moviesFolder) && Directory.Exists(moviesFolder))
        {
            _moviesWatcher = CreateWatcher(moviesFolder);
        }

        if (!string.IsNullOrWhiteSpace(seriesFolder) && Directory.Exists(seriesFolder))
        {
            _seriesWatcher = CreateWatcher(seriesFolder);
        }
    }

    /// <summary>Останавливает отслеживание изменений.</summary>
    public void StopWatching()
    {
        _moviesWatcher?.Dispose();
        _moviesWatcher = null;
        _seriesWatcher?.Dispose();
        _seriesWatcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    /// <summary>Создаёт наблюдатель файловой системы для указанной папки.</summary>
    private FileSystemWatcher CreateWatcher(string folder)
    {
        var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        watcher.Created += OnFileSystemEvent;
        watcher.Deleted += OnFileSystemEvent;
        watcher.Renamed += OnFileSystemEvent;
        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    /// <summary>
    /// Обработчик событий ФС: через «дебаунс» (задержка 1,5 с) поднимает событие LibraryChanged,
    /// чтобы не сканировать библиотеку на каждое из множества промежуточных событий.
    /// </summary>
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        _debounceLock.Wait();
        try
        {
            _debounceTimer ??= new System.Timers.Timer(1500) { AutoReset = false };
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
        finally
        {
            _debounceLock.Release();
        }

        _debounceTimer!.Elapsed += (_, _) => LibraryChanged?.Invoke();
    }

    public void Dispose()
    {
        StopWatching();
        _debounceLock.Dispose();
    }
}
