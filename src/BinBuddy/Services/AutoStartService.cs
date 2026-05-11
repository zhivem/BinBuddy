using Microsoft.Win32;

namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для управления автозапуском приложения
/// </summary>
public class AutoStartService
{
    private const string AppName = "RecycleBinManager";
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _appPath;

    public AutoStartService()
    {
        _appPath = System.Windows.Forms.Application.ExecutablePath;
    }

    /// <summary>
    /// Проверяет, включен ли автозапуск
    /// </summary>
    public bool IsEnabled() => GetRegistryValue()?.Equals(_appPath, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Включает автозапуск
    /// </summary>
    public void Enable()
    {
        using var key = GetRegistryKey(true);
        key?.SetValue(AppName, _appPath, RegistryValueKind.String);
    }

    /// <summary>
    /// Отключает автозапуск
    /// </summary>
    public void Disable()
    {
        using var key = GetRegistryKey(true);
        key?.DeleteValue(AppName, false);
    }

    /// <summary>
    /// Переключает состояние автозапуска
    /// </summary>
    public void Toggle()
    {
        if (IsEnabled())
            Disable();
        else
            Enable();
    }

    /// <summary>
    /// Возвращает статус автозапуска
    /// </summary>
    public string GetStatus() => IsEnabled() ? "Включен" : "Отключен";

    private string? GetRegistryValue()
    {
        using var key = GetRegistryKey(false);
        return key?.GetValue(AppName) as string;
    }

    private RegistryKey? GetRegistryKey(bool writable) =>
        Registry.CurrentUser.OpenSubKey(RegistryPath, writable);
}
