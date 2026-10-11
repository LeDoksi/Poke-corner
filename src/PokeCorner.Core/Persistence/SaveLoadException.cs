namespace PokeCorner.Core.Persistence;

/// A recoverable read failure; never treat this as an absent save or start a new game.
public sealed class SaveLoadException : IOException
{
    public string FilePath { get; }

    public SaveLoadException(string filePath, string diagnostic, Exception? innerException = null)
        : base($"Не удалось загрузить сохранение '{filePath}': {diagnostic}. Файл не изменён.", innerException)
    {
        FilePath = filePath;
    }
}
