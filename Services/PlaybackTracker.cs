using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Отслеживание запущенной серии: через заданное время ставит отметку «просмотрено».
/// Плеер внешний, поэтому точное время воспроизведения недоступно —
/// считаем время с момента запуска файла.
/// </summary>
public class PlaybackTracker : IDisposable
{
    private readonly WatchHistoryService _history;
    private CancellationTokenSource? _cts;

    public PlaybackTracker(WatchHistoryService history)
    {
        _history = history;
    }

    /// <summary>Серия отмечена просмотренной (для обновления интерфейса).</summary>
    public event EventHandler<EpisodeWatchedEventArgs>? Watched;

    /// <summary>Начинает отслеживание запущенной серии согласно настройке.</summary>
    public void Start(Episode episode, string seriesFolder, AutoMarkMode mode, int thresholdMinutes)
    {
        // Одновременно отслеживается только последняя запущенная серия
        Cancel();

        if (mode == AutoMarkMode.Off)
        {
            return;
        }

        if (mode == AutoMarkMode.OnLaunch)
        {
            MarkWatched(episode, seriesFolder);
            return;
        }

        _history.SetState(seriesFolder, episode, WatchState.InProgress);

        var threshold = TimeSpan.FromMinutes(Math.Clamp(thresholdMinutes, 1, 180));
        _cts = new CancellationTokenSource();
        _ = TrackAsync(episode, seriesFolder, threshold, _cts.Token);
    }

    /// <summary>Отменяет отложенную автопометку (например, при ручном переключении).</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task TrackAsync(Episode episode, string seriesFolder, TimeSpan threshold,
        CancellationToken ct)
    {
        var startedAt = Environment.TickCount64;

        try
        {
            // Тикаем по 5 секунд: так сон/пробуждение системы не сдвигают отметку,
            // а таймер остаётся отменяемым
            while (true)
            {
                var elapsed = Environment.TickCount64 - startedAt;
                var left = threshold.TotalMilliseconds - elapsed;
                if (left <= 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(5000, left)), ct);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        MarkWatched(episode, seriesFolder);
    }

    private void MarkWatched(Episode episode, string seriesFolder)
    {
        _history.SetState(seriesFolder, episode, WatchState.Watched);
        Watched?.Invoke(this, new EpisodeWatchedEventArgs(seriesFolder, episode.Path));
    }

    public void Dispose() => Cancel();
}

/// <summary>Данные о серии, отмеченной просмотренной автоматически.</summary>
public class EpisodeWatchedEventArgs : EventArgs
{
    public EpisodeWatchedEventArgs(string seriesFolder, string episodePath)
    {
        SeriesFolder = seriesFolder;
        EpisodePath = episodePath;
    }

    /// <summary>Папка сериала.</summary>
    public string SeriesFolder { get; }

    /// <summary>Путь к файлу серии.</summary>
    public string EpisodePath { get; }
}
