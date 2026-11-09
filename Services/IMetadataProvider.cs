using CinemaHall.Models;

namespace CinemaHall.Services;

/// <summary>
/// Абстракция провайдера метаданных (постеры, описания).
/// Позволяет при необходимости заменить Кинопоиск на другой сервис.
/// </summary>
public interface IMetadataProvider
{
    /// <summary>
    /// Ищет тайтл по названию и году и заполняет метаданные.
    /// Возвращает false, если ничего не найдено.
    /// </summary>
    Task<bool> TryFillAsync(MediaItem item, CancellationToken ct = default);
}
