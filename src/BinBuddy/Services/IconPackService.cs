using System.NativeTray;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для управления пакетами иконок корзины
/// </summary>
public class IconPackService : IDisposable
{
    private readonly SettingsService _settingsService;
    private Icon? _emptyIcon;
    private Icon? _fullIcon;
    private readonly object _iconLock = new();
    private bool _disposed;

    public IconPackService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Применяет пакет иконок к системному трею
    /// </summary>
    public void ApplyIconPack(string packName, TrayIconHost trayIcon)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);

        string emptyIconPath = GetIconPath(packName, "recycle-empty.ico");
        string fullIconPath = GetIconPath(packName, "recycle-full.ico");

        if (!File.Exists(emptyIconPath) || !File.Exists(fullIconPath))
            return;

        lock (_iconLock)
        {
            _emptyIcon?.Dispose();
            _fullIcon?.Dispose();

            _emptyIcon = new Icon(emptyIconPath);
            _fullIcon = new Icon(fullIconPath);
        }

        trayIcon.Icon = (IsRecycleBinEmpty() ? _emptyIcon : _fullIcon).Handle;
        SaveCurrentPack(packName);
    }

    /// <summary>
    /// Обновляет иконку в зависимости от состояния корзины
    /// </summary>
    public void UpdateIconsBasedOnState(TrayIconHost trayIcon, bool isEmpty)
    {
        ArgumentNullException.ThrowIfNull(trayIcon);

        if (isEmpty && _emptyIcon != null)
            trayIcon.Icon = _emptyIcon.Handle;
        else if (!isEmpty && _fullIcon != null)
            trayIcon.Icon = _fullIcon.Handle;
    }

    /// <summary>
    /// Загружает имя текущего пакета иконок
    /// </summary>
    public string LoadCurrentPack() => _settingsService.LoadSettings().CurrentIconPack ?? "default";

    private static string GetIconPath(string packName, string iconName) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName, iconName);

    private void SaveCurrentPack(string packName)
    {
        var settings = _settingsService.LoadSettings();
        settings.CurrentIconPack = packName;
        _settingsService.SaveSettings(settings);
    }

    private static bool IsRecycleBinEmpty()
    {
        var rbInfo = new SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>() };
        SHQueryRecycleBin(null, ref rbInfo);
        return rbInfo.i64NumItems == 0;
    }

    /// <summary>
    /// Освобождает ресурсы иконок
    /// </summary>
    public void DisposeIcons()
    {
        lock (_iconLock)
        {
            _emptyIcon?.Dispose();
            _fullIcon?.Dispose();
            _emptyIcon = null;
            _fullIcon = null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisposeIcons();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }
}
