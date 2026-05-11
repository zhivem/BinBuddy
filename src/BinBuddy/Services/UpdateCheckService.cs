namespace BinBuddy.src.BinBuddy.Services;

/// <summary>
/// Сервис для проверки доступности обновлений приложения
/// </summary>
public class UpdateCheckService : IDisposable
{
    private const string VersionUrl = "https://raw.githubusercontent.com/zhivem/BinBuddy/main/version.txt";
    private const string ReleasesUrl = "https://github.com/zhivem/BinBuddy/releases";

    private readonly HttpClient _httpClient = new();
    private string? _latestVersion;
    private bool _disposed;

    /// <summary>
    /// Проверяет наличие обновлений
    /// </summary>
    public async Task<bool> IsUpdateAvailableAsync()
    {
        try
        {
            string currentVersion = GetCurrentVersion();
            string? latestVersion = await GetLatestVersionAsync();

            if (string.IsNullOrEmpty(latestVersion))
                return false;

            _latestVersion = latestVersion;
            return CompareVersions(latestVersion, currentVersion) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Открывает страницу релизов в браузере
    /// </summary>
    public void OpenReleasesPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>
    /// Возвращает последнюю известную версию
    /// </summary>
    public string? GetLatestVersion() => _latestVersion;

    private async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            return (await _httpClient.GetStringAsync(VersionUrl)).Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string GetCurrentVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString(3) ?? "1.0";
    }

    private static int CompareVersions(string versionA, string versionB)
    {
        var partsA = versionA.Split('.');
        var partsB = versionB.Split('.');

        for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
        {
            int numA = i < partsA.Length && int.TryParse(partsA[i], out int a) ? a : 0;
            int numB = i < partsB.Length && int.TryParse(partsB[i], out int b) ? b : 0;

            if (numA != numB)
                return numA.CompareTo(numB);
        }

        return 0;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
