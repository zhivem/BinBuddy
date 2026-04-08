using System.Diagnostics;
using System.Text.Json;

namespace RecycleBinManager;

internal static class SettingsManager
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RecycleBinManager",
        "settings.json"
    );

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static AppSettings? _cachedSettings;
    private static readonly object _lock = new();

    public static AppSettings LoadSettings()
    {
        lock (_lock)
        {
            if (_cachedSettings is not null)
                return _cachedSettings;

            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    var defaultSettings = new AppSettings();
                    SaveSettings(defaultSettings);
                    _cachedSettings = defaultSettings;
                    return defaultSettings;
                }

                var json = File.ReadAllText(SettingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                return _cachedSettings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
                _cachedSettings = new AppSettings();
                return _cachedSettings;
            }
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFilePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }
    }

    public static void ReloadSettings()
    {
        lock (_lock)
        {
            _cachedSettings = null;
            LoadSettings();
        }
    }

    public static void ResetToDefaults()
    {
        var defaultSettings = new AppSettings();
        SaveSettings(defaultSettings);
    }
}

public class AppSettings
{
    public bool ShowNotifications { get; set; } = true;
    public bool ShowRecycleBinOnDesktop { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = false;
    public string CurrentIconPack { get; set; } = "default";
    public int UpdateIntervalSeconds { get; set; } = 1;

    // Метод для создания копии настроек
    public AppSettings Clone() => new()
    {
        ShowNotifications = this.ShowNotifications,
        ShowRecycleBinOnDesktop = this.ShowRecycleBinOnDesktop,
        AutoStartEnabled = this.AutoStartEnabled,
        CurrentIconPack = this.CurrentIconPack,
        UpdateIntervalSeconds = this.UpdateIntervalSeconds
    };

    // Метод для проверки валидности
    public bool IsValid()
    {
        return UpdateIntervalSeconds is >= 1 and <= 60
               && !string.IsNullOrEmpty(CurrentIconPack);
    }

    // Метод для нормализации значений
    public void Normalize()
    {
        UpdateIntervalSeconds = Math.Clamp(UpdateIntervalSeconds, 1, 60);
        CurrentIconPack ??= "default";
    }
}