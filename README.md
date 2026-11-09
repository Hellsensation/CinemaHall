# CinemaHall

WPF-приложение ( .NET 10, Windows) — локальный каталог фильмов и сериалов в стиле стриминговых сервисов: плитки с постерами, описания, рейтинги и жанры автоматически подтягиваются из Кинопоиска, воспроизведение — через системный плеер по умолчанию.

## Возможности

- **Каталог из локальных файлов**: сканирует указанные папки и строит «афишу» из плиток с постерами.
- **Автоматические метаданные**: название, год, описание, постер, рейтинг и жанры загружаются с [Kinopoisk API Unofficial](https://kinopoiskapiunofficial.tech).
- **Дисковый кэш метаданных** — экономит квоту бесплатного API (200 запросов/сутки) и ускоряет повторные запуски.
- **Умный парсер имён файлов**: из `Начало.2010.BDRip.1080p.x264` извлекает `«Начало» (2010)`, отбрасывая теги качества, кодеков, релиз-групп и метки сезонов ([TitleParser.cs](Services/TitleParser.cs)).
- **Отслеживание изменений**: `FileSystemWatcher` следит за папками библиотеки — новые файлы появляются в каталоге без перезапуска (с защитой от «дребезга» событий).
- **Воспроизведение**: фильм открывается системным плеером; сериал — первый найденный эпизод (если видеофайлов нет, открывается папка в Проводнике) ([PlayerLauncher.cs](Services/PlayerLauncher.cs)).

## Структура библиотеки

Приложение ожидает два вида организации файлов:

| Тип | Организация |
|---|---|
| **Фильмы** | видеофайлы лежат **в корне** папки фильмов: `Фильмы/Начало.2010.mkv` |
| **Сериалы** | каждая **подпапка** — отдельный сериал: `Сериалы/The Leftovers 2014 S01/эпизоды...` |

Поддерживаемые видеоформаты: `.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.m4v`, `.mpg`, `.mpeg`, `.ts`.

## Где что хранится

Все данные приложения лежат в `%LocalAppData%\CinemaHall\` (т.е. обычно `C:\Users\<пользователь>\AppData\Local\CinemaHall\`):

| Путь | Назначение | Код |
|---|---|---|
| `settings.json` | **Настройки**: папка фильмов, папка сериалов, API-ключ Кинопоиска | [SettingsService.cs](Services/SettingsService.cs) |
| `log.txt` | **Логи**: диагностический журнал. Перезаписывается при каждом запуске; сюда же пишутся ошибки привязок WPF (`[БИНДИНГ]`) | [Log.cs](Services/Log.cs), [App.xaml.cs](App.xaml.cs) |
| `cache\<SHA256>\` | **Кэш метаданных**: для каждого элемента — `meta.json` + `poster.jpg` | [MetadataCache.cs](Services/MetadataCache.cs) |

## Первый запуск

1. Соберите и запустите приложение (см. ниже).
2. При первом запуске автоматически откроется окно **Настройки** (если API-ключ или папки не заданы).
3. Получите бесплатный API-ключ на [kinopoiskapiunofficial.tech](https://kinopoiskapiunofficial.tech) (200 запросов/сутки) и укажите его.
4. Укажите папку с фильмами и/или папку с сериалами.
5. Каталог построится автоматически: сначала метаданные берутся из кэша, затем — из API (с ограничением частоты 5 запросов/сек).

## Сборка и запуск

```cmd
dotnet build CinemaHall.csproj
dotnet run --project CinemaHall.csproj
```

Требования: Windows 10/11, .NET 10 SDK, рабочая среда — WPF (`UseWPF`).

## Архитектура

MVVM с [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/):

```
Models/        — доменные модели: MediaItem, AppSettings, DTO Кинопоиска
Services/      — вся логика:
    LibraryScanner    — сканирование папок + FileSystemWatcher
    TitleParser       — извлечение названия/года из имён файлов
    KinopoiskProvider — загрузка метаданных (rate limiting, поиск + детали)
    MetadataCache     — дисковый кэш (SHA256-хэш пути → папка кэша)
    SettingsService   — settings.json
    Log               — файловый логгер
    PlayerLauncher    — запуск в системном плеере
ViewModels/    — MainViewModel (каталог, команды), MediaItemViewModel, SettingsViewModel
Views/         — MainWindow (плитки, панель описания), SettingsWindow
Converters/    — PosterImageConverter (локальный постер → ImageSource)
Assets/        — иконка приложения (app.ico, размеры 16–256 px)
tools/         — служебные скрипты:
    make-icon.ps1 — генерация Assets/app.ico из programIcon.png
                    (удаление фона flood fill, набор размеров 16/24/32/48/64/256)
```

**Поток данных при построении каталога** ([MainViewModel.RefreshAsync](ViewModels/MainViewModel.cs:56)):

```
папки библиотеки → LibraryScanner.Scan → TitleParser.Parse
    → MetadataCache.TryLoad (кэш)
    → KinopoiskProvider.TryFillAsync (API, если в кэше нет)
    → коллекция плиток (ObservableCollection)
```

## Диагностика проблем

- Приложение «не видит» фильмы — проверьте, что файлы лежат в корне папки фильмов, а расширения входят в список поддерживаемых.
- Постеры/описания не загружаются — проверьте API-ключ в `settings.json` и остаток квоты; детали поиска — в `log.txt` (строки с `[API]`).
- Кэш повреждён/устарел — просто удалите папку `%LocalAppData%\CinemaHall\cache\`, метаданные будут загружены заново.
