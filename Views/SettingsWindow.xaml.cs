using Microsoft.Win32;
using System.Windows;
using CinemaHall.ViewModels;

namespace CinemaHall.Views;

/// <summary>
/// Диалог настроек: выбор папок каталога и ввод API-ключа.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Выбор папки с фильмами.</summary>
    private void BrowseMoviesFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(folder =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.MoviesFolder = folder;
            }
        });

    /// <summary>Выбор папки с сериалами.</summary>
    private void BrowseSeriesFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(folder =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.SeriesFolder = folder;
            }
        });

    /// <summary>Показывает диалог выбора папки и вызывает продолжение при успехе.</summary>
    private void BrowseFolder(Action<string> onPicked)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку"
        };

        if (dialog.ShowDialog(this) == true)
        {
            onPicked(dialog.FolderName);
        }
    }
}
