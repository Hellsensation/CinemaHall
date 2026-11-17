namespace CinemaHall.Models;

/// <summary>
/// Состояние серии с точки зрения просмотра.
/// </summary>
public enum WatchState
{
    /// <summary>Серия не просмотрена.</summary>
    None,

    /// <summary>Просмотр начат, но отметка «просмотрено» ещё не поставлена.</summary>
    InProgress,

    /// <summary>Серия просмотрена.</summary>
    Watched
}

/// <summary>
/// История просмотра одной серии.
/// </summary>
public class EpisodeProgress
{
    /// <summary>Текущее состояние просмотра.</summary>
    public WatchState State { get; set; }

    /// <summary>
    /// Когда серия была запущена. Нужно, чтобы досчитать просмотр
    /// после закрытия приложения, не дождавшегося автопометки.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Когда состояние менялось в последний раз.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
