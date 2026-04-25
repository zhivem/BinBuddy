using System.Diagnostics;

namespace BinBuddy.src.BinBuddy
{
    public static class UpdateChecker
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/zhivem/BinBuddy/main/version.txt";
        private const string ReleasesUrl = "https://github.com/zhivem/BinBuddy/releases";

        private static readonly HttpClient _httpClient = new HttpClient();
        private static string? _latestVersion;

        public static async Task<string?> GetLatestVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(VersionUrl);
                return response.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> IsUpdateAvailableAsync()
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

        public static void OpenReleasesPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
            }
            catch
            {
                // Если не удалось открыть браузер
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

        public static string? GetLatestVersion()
        {
            return _latestVersion;
        }
    }
}