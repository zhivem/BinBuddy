using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для управления операциями корзины (информация, очистка)
/// </summary>
public class RecycleBinService
{
    /// <summary>
    /// Информация о состоянии корзины
    /// </summary>
    public struct RecycleBinInfo
    {
        public long ItemCount;
        public long SizeInBytes;
    }

    /// <summary>
    /// Получает информацию о корзине
    /// </summary>
    public RecycleBinInfo GetRecycleBinInfo()
    {
        var rbInfo = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
        SHQueryRecycleBin(null, ref rbInfo);
        
        return new RecycleBinInfo
        {
            ItemCount = rbInfo.i64NumItems,
            SizeInBytes = rbInfo.i64Size
        };
    }

    /// <summary>
    /// Проверяет, пуста ли корзина
    /// </summary>
    public bool IsEmpty() => GetRecycleBinInfo().ItemCount == 0;

    /// <summary>
    /// Очищает корзину
    /// </summary>
    /// <returns>True, если очистка прошла успешно</returns>
    public bool Empty()
    {
        const uint flags = 0x00000001 | 0x00000002 | 0x00000004; // NOCONFIRMATION | NOPROGRESSUI | NOSOUND
        return SHEmptyRecycleBin(IntPtr.Zero, null, flags) == 0;
    }

    /// <summary>
    /// Форматирует размер в читаемый формат
    /// </summary>
    public static string FormatFileSize(long sizeInBytes)
    {
        string[] sizes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double len = sizeInBytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }
}
