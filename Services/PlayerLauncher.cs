using System.Diagnostics;
using System.IO;
using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Запуск воспроизведения через стандартный системный плеер.
/// </summary>
public static class PlayerLauncher
{
    /// <summary>
    /// Открывает элемент каталога в системном плеере по умолчанию.
    /// Для фильма — сам файл; для сериала — первый найденный эпизод
    /// (если эпизодов нет, открывается папка сериала в Проводнике).
    /// </summary>
    public static void Play(MediaItem item)
    {
        var path = item.Path;

        if (Directory.Exists(path))
        {
            // Сериал: ищем первый эпизод во вложенных папках
            var firstEpisode = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(IsVideoFile)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (firstEpisode is null)
            {
                // Видеофайлов нет — просто открываем папку
                OpenInExplorer(path);
                return;
            }

            path = firstEpisode;
        }

        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        else
        {
            OpenInExplorer(path);
        }
    }

    /// <summary>
    /// Запускает воспроизведение конкретного файла (например, выбранной серии)
    /// через системный плеер по умолчанию.
    /// </summary>
    public static void PlayFile(string path)
    {
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        else if (Directory.Exists(path))
        {
            OpenInExplorer(path);
        }
    }

    /// <summary>Открывает папку в Проводнике Windows.</summary>
    private static void OpenInExplorer(string folder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true
        });
    }

    private static bool IsVideoFile(string path) =>
        LibraryScanner.IsVideoFile(path);
}
