using System.Diagnostics;
using System.Windows;
using CinemaHall.Services;
using CinemaHall.ViewModels;
using CinemaHall.Views;

namespace CinemaHall;

/// <summary>
/// Точка входа WPF-приложения.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Очищаем диагностический лог от прошлого запуска
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CinemaHall", "log.txt");
            if (System.IO.File.Exists(logPath))
            {
                System.IO.File.Delete(logPath);
            }
        }
        catch { }

        // Диагностика: все ошибки привязок WPF пишем в лог
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingTraceListener());
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        // Создаём и показываем главное окно с главным ViewModel
        var main = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        MainWindow = main;
        main.Show();
    }
}

/// <summary>Прослушиватель ошибок привязок WPF, пишет их в файловый лог.</summary>
public class BindingTraceListener : TraceListener
{
    public override void Write(string? message) { }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Log.Write($"[БИНДИНГ] {message}");
        }
    }
}
