using System.Windows;
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

        // Создаём и показываем главное окно с главным ViewModel
        var main = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        MainWindow = main;
        main.Show();
    }
}
