namespace CinemaHall.Models;

/// <summary>
/// Способ, которым приложение отмечает серии просмотренными.
/// </summary>
public enum AutoMarkMode
{
    /// <summary>Только вручную: отметка ставится кликом по значку у серии.</summary>
    Off,

    /// <summary>Сразу при запуске серии во внешнем плеере.</summary>
    OnLaunch,

    /// <summary>Через заданное в настройках число минут после запуска серии.</summary>
    AfterThreshold
}
