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

        // Создаём и показываем главное окно с главным ViewModel
        var main = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        MainWindow = main;
        main.Show();
    }
}
