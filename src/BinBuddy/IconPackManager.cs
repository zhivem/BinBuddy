using System.Runtime.InteropServices;
using System.NativeTray;

namespace BinBuddy.src.BinBuddy
{
    public static class IconPackManager
    {
        private static string _currentPack = "default";
        private static Icon? _emptyIcon;
        private static Icon? _fullIcon;
        private static readonly object _iconLock = new();

        public static void ApplyIconPack(string packName, TrayIconHost trayIcon)
        {
            ArgumentNullException.ThrowIfNull(trayIcon);

            string packPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName);
            string emptyIconPath = Path.Combine(packPath, "recycle-empty.ico");
            string fullIconPath = Path.Combine(packPath, "recycle-full.ico");

            if (File.Exists(emptyIconPath) && File.Exists(fullIconPath))
            {
                lock (_iconLock)
                {
                    _emptyIcon?.Dispose();
                    _fullIcon?.Dispose();

                    _emptyIcon = new Icon(emptyIconPath);
                    _fullIcon = new Icon(fullIconPath);
                }

                bool isRecycleBinEmpty = IsRecycleBinEmpty();
                trayIcon.Icon = (isRecycleBinEmpty ? _emptyIcon : _fullIcon).Handle;

                _currentPack = packName;
                SaveCurrentPack(packName);
            }
        }

        public static void UpdateIconsBasedOnState(TrayIconHost trayIcon, bool isEmpty)
        {
            ArgumentNullException.ThrowIfNull(trayIcon);

            if (isEmpty && _emptyIcon != null)
            {
                trayIcon.Icon = _emptyIcon.Handle;
            }
            else if (!isEmpty && _fullIcon != null)
            {
                trayIcon.Icon = _fullIcon.Handle;
            }
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

            SHQueryRecycleBin(null, ref rbInfo);
            return rbInfo.i64NumItems == 0;
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
}