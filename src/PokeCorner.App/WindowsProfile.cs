using System.IO;
using System.Runtime.InteropServices;

namespace PokeCorner.App;

public static class WindowsProfile
{
    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProfileType(out uint flags);

    public static void EnsurePersistent(Func<(bool Success, uint Flags, int Error)>? query = null)
    {
        var result = (query ?? Query)();
        if (!result.Success) throw new IOException($"Windows не подтвердил постоянный профиль (ошибка {result.Error}). Загрузка и запись игры запрещены.");
        // WinSDK PT_TEMPORARY=0x1: Windows deletes this profile when the user logs off.
        if ((result.Flags & 0x1) != 0)
            throw new IOException("Windows вошёл во временный профиль, который удаляется при выходе. Войдите в постоянный профиль; новая игра и запись сохранений запрещены.");
    }

    private static (bool Success, uint Flags, int Error) Query()
    {
        try { var success = GetProfileType(out var flags); return (success, flags, Marshal.GetLastWin32Error()); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        { throw new IOException("Windows не может проверить тип профиля. Загрузка и запись игры запрещены.", ex); }
    }
}
