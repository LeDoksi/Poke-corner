namespace PokeCorner.Core.Persistence;

/// OS-held lease shared across Windows sessions; never delete the lock file.
public sealed class SaveProfileLease : IDisposable
{
    private readonly FileStream _stream;
    private SaveProfileLease(FileStream stream) => _stream = stream;
    public static SaveProfileLease Acquire(string directory)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new IOException("Путь профиля должен быть абсолютным.");
        Directory.CreateDirectory(directory);
        return new(new FileStream(Path.Combine(directory, "save-profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
    }
    public void Dispose() => _stream.Dispose();
}
