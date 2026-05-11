using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для управления видимостью корзины на рабочем столе
/// </summary>
public class RecycleBinVisibilityService
{
    private const string DesktopKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    private const string RecycleBinValue = "{645FF040-5081-101B-9F08-00AA002F954E}";

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_FLUSH = 0x1000;

    /// <summary>
    /// Проверяет, видна ли корзина на рабочем столе
    /// </summary>
    public bool IsVisible() => Registry.GetValue(DesktopKey, RecycleBinValue, 0) is 0;

    /// <summary>
    /// Устанавливает видимость корзины
    /// </summary>
    public void SetVisibility(bool isVisible)
    {
        Registry.SetValue(DesktopKey, RecycleBinValue, isVisible ? 0 : 1, RegistryValueKind.DWord);
        RefreshDesktop();
    }

    /// <summary>
    /// Показывает корзину на рабочем столе
    /// </summary>
    public void Show() => SetVisibility(true);

    /// <summary>
    /// Скрывает корзину с рабочего стола
    /// </summary>
    public void Hide() => SetVisibility(false);

    /// <summary>
    /// Переключает видимость корзины
    /// </summary>
    public void Toggle() => SetVisibility(!IsVisible());

    /// <summary>
    /// Возвращает статус видимости корзины
    /// </summary>
    public string GetVisibilityStatus() => IsVisible() ? "Видна" : "Скрыта";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private void RefreshDesktop() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
}
