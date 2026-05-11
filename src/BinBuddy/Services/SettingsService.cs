using System.Text.Json;

namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для управления настройками приложения
/// </summary>
public class SettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppSettings? _cachedSettings;
    private readonly object _lock = new();

    public SettingsService()
    {
        _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RecycleBinManager",
            "settings.json"
        );

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Загружает настройки из файла или создает новые
    /// </summary>
    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            if (_cachedSettings is not null)
                return _cachedSettings;

            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _cachedSettings = new AppSettings();
                    SaveSettings(_cachedSettings);
                    return _cachedSettings;
                }

                var json = File.ReadAllText(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                _cachedSettings.Normalize();
                return _cachedSettings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
                return _cachedSettings = new AppSettings();
            }
        }
    }

    /// <summary>
    /// Сохраняет настройки в файл
    /// </summary>
    public void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
        {
            try
            {
                string directory = Path.GetDirectoryName(_settingsFilePath)!;
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Перезагружает настройки из файла
    /// </summary>
    public void ReloadSettings()
    {
        lock (_lock)
        {
            _cachedSettings = null;
            LoadSettings();
        }
    }

    /// <summary>
    /// Сбрасывает настройки к значениям по умолчанию
    /// </summary>
    public void ResetToDefaults() => SaveSettings(new AppSettings());
}
