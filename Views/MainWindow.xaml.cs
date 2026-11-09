using System.Windows;
using CinemaHall.Services;
using CinemaHall.ViewModels;

namespace CinemaHall.Views;

/// <summary>
/// Главное окно: плитки афиш, панель описания, кнопка «Смотреть».
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.SettingsRequested += OnSettingsRequested;

        // При первом запуске (нет ключа или папок) сначала открываем настройки
        var settings = new SettingsService().Load();
        if (string.IsNullOrWhiteSpace(settings.KinopoiskApiKey) ||
            (string.IsNullOrWhiteSpace(settings.MoviesFolder) &&
             string.IsNullOrWhiteSpace(settings.SeriesFolder)))
        {
            ShowSettingsDialog();
        }

        await vm.InitializeAsync();
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettingsDialog();

    /// <summary>Открывает диалог настроек; после сохранения перестраивает каталог.</summary>
    private void ShowSettingsDialog()
    {
        var vm = new SettingsViewModel(new SettingsService());
        var dialog = new SettingsWindow
        {
            Owner = this,
            DataContext = vm
        };
        dialog.ShowDialog();

        if (vm.Saved)
        {
            _ = ViewModel?.InitializeAsync();
        }
    }
}
