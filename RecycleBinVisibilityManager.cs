using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace RecycleBinManager;

public static class RecycleBinVisibilityManager
{
    private const string DesktopKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    private const string RecycleBinValue = "{645FF040-5081-101B-9F08-00AA002F954E}";

    // Константы для SHChangeNotify
    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_FLUSH = 0x1000;

    public static bool IsRecycleBinVisibleOnDesktop()
    {
        object? value = Registry.GetValue(DesktopKey, RecycleBinValue, 0);
        return Convert.ToInt32(value) == 0;
    }

    public static void SetRecycleBinVisibilityOnDesktop(bool isVisible)
    {
        int value = isVisible ? 0 : 1;
        Registry.SetValue(DesktopKey, RecycleBinValue, value, RegistryValueKind.DWord);
        RefreshDesktopIcons();
    }

    public static void ShowRecycleBin() => SetRecycleBinVisibilityOnDesktop(true);
    public static void HideRecycleBin() => SetRecycleBinVisibilityOnDesktop(false);

    public static void ToggleRecycleBinVisibility()
    {
        SetRecycleBinVisibilityOnDesktop(!IsRecycleBinVisibleOnDesktop());
    }

    public static string GetVisibilityStatus()
    {
        return IsRecycleBinVisibleOnDesktop() ? "Видна" : "Скрыта";
    }

    private static void RefreshDesktopIcons()
    {
        // Уведомляем систему об изменении иконок рабочего стола
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}