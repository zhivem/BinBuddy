namespace BinBuddy.src.BinBuddy.Models;

/// <summary>
/// Представляет настройки приложения
/// </summary>
public class AppSettings
{
    public bool ShowNotifications { get; set; } = true;
    public bool ShowRecycleBinOnDesktop { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = false;
    public string CurrentIconPack { get; set; } = "default";
    public int UpdateIntervalSeconds { get; set; } = 1;

    /// <summary>
    /// Создает копию текущих настроек
    /// </summary>
    public AppSettings Clone() => new()
    {
        ShowNotifications = ShowNotifications,
        ShowRecycleBinOnDesktop = ShowRecycleBinOnDesktop,
        AutoStartEnabled = AutoStartEnabled,
        CurrentIconPack = CurrentIconPack,
        UpdateIntervalSeconds = UpdateIntervalSeconds
    };

    /// <summary>
    /// Нормализует значения настроек до допустимых диапазонов
    /// </summary>
    public void Normalize()
    {
        UpdateIntervalSeconds = Math.Clamp(UpdateIntervalSeconds, 1, 60);
        CurrentIconPack ??= "default";
    }
}
