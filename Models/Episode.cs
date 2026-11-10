namespace CinemaHall.Models;

/// <summary>
/// Серия (эпизод) сериала: отдельный видеофайл в папке сериала.
/// </summary>
public class Episode
{
    /// <summary>Полный путь к видеофайлу серии.</summary>
    public required string Path { get; set; }

    /// <summary>Имя файла серии с расширением.</summary>
    public required string FileName { get; set; }

    /// <summary>Очищенное название серии (из имени файла).</summary>
    public required string Name { get; set; }

    /// <summary>Номер сезона (если удалось распознать, например из «S01E05»).</summary>
    public int? Season { get; set; }

    /// <summary>Номер эпизода в сезоне (если удалось распознать).</summary>
    public int? Number { get; set; }
}
