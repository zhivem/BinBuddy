using System.Runtime.InteropServices;
using NotifyIconEx;
using NotifyIconExAlias = NotifyIconEx.NotifyIcon;

namespace RecycleBinManager;

public static class IconPackManager
{
    private static string _currentPack = "default";
    private static Icon? _emptyIcon;
    private static Icon? _fullIcon;
    private static readonly object _iconLock = new();

    public static void ApplyIconPack(string packName, NotifyIconExAlias notifyIcon)
    {
        ArgumentNullException.ThrowIfNull(notifyIcon);

        string packPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName);
        string emptyIconPath = Path.Combine(packPath, "recycle-empty.ico");
        string fullIconPath = Path.Combine(packPath, "recycle-full.ico");

        if (File.Exists(emptyIconPath) && File.Exists(fullIconPath))
        {
            lock (_iconLock)
            {
                // Освобождаем старые иконки перед загрузкой новых
                _emptyIcon?.Dispose();
                _fullIcon?.Dispose();

                _emptyIcon = new Icon(emptyIconPath);
                _fullIcon = new Icon(fullIconPath);
            }

            bool isRecycleBinEmpty = IsRecycleBinEmpty();
            notifyIcon.Icon = isRecycleBinEmpty ? _emptyIcon : _fullIcon;

            _currentPack = packName;
            SaveCurrentPack(packName);
        }
        else
        {
            notifyIcon.Icon = SystemIcons.Application;
        }
    }

    public static void UpdateIconsBasedOnState(NotifyIconExAlias notifyIcon, bool isEmpty)
    {
        ArgumentNullException.ThrowIfNull(notifyIcon);

        if (isEmpty && _emptyIcon != null)
        {
            notifyIcon.Icon = _emptyIcon;
        }
        else if (!isEmpty && _fullIcon != null)
        {
            notifyIcon.Icon = _fullIcon;
        }
        else
        {
            notifyIcon.Icon = SystemIcons.Application;
        }
    }

    public static ToolStripMenuItem CreateIconPackMenuItem(string packName, NotifyIconExAlias notifyIcon)
    {
        ArgumentNullException.ThrowIfNull(notifyIcon);

        var menuItem = new ToolStripMenuItem(packName);

        // Загружаем иконку для пункта меню (уменьшенный размер)
        using var icon = LoadIconForPack(packName);
        if (icon != null)
        {
            menuItem.Image = icon.ToBitmap();
            menuItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;
        }

        menuItem.Click += (_, _) =>
        {
            ApplyIconPack(packName, notifyIcon);
            bool isRecycleBinEmpty = IsRecycleBinEmpty();
            UpdateIconsBasedOnState(notifyIcon, isRecycleBinEmpty);
        };

        return menuItem;
    }

    private static Icon? LoadIconForPack(string packName)
    {
        string packPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName);
        string iconPath = Path.Combine(packPath, "recycle-empty.ico");

        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return null;
    }

    private static void SaveCurrentPack(string packName)
    {
        var settings = SettingsManager.LoadSettings();
        settings.CurrentIconPack = packName;
        SettingsManager.SaveSettings(settings);
    }

    public static string LoadCurrentPack()
    {
        var settings = SettingsManager.LoadSettings();
        return settings.CurrentIconPack ?? "default";
    }

    public static void DisposeIcons()
    {
        lock (_iconLock)
        {
            _emptyIcon?.Dispose();
            _fullIcon?.Dispose();
            _emptyIcon = null;
            _fullIcon = null;
        }
    }

    private static bool IsRecycleBinEmpty()
    {
        var rbInfo = new SHQUERYRBINFO
        {
            cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>()
        };

        // Используем DllImport вместо LibraryImport для простоты
        SHQueryRecycleBin(null, ref rbInfo);
        return rbInfo.i64NumItems == 0;
    }

    // Возвращаемся к классическому DllImport, который работает без partial
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