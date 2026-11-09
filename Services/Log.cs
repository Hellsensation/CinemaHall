using System.IO;

namespace CinemaHall.Services;

/// <summary>
/// Простой файловый логгер для диагностики: %LocalAppData%\CinemaHall\log.txt.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CinemaHall",
        "log.txt");

    /// <summary>Пишет строку в лог с меткой времени.</summary>
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Лог не должен ломать работу приложения
        }
    }
}
