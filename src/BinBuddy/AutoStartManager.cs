using Microsoft.Win32;

namespace BinBuddy.src.BinBuddy
{
    public static class AutoStartManager
    {
        private const string AppName = "RecycleBinManager";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly string AppPath = Application.ExecutablePath;

        public static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
            return string.Equals(key?.GetValue(AppName) as string, AppPath, StringComparison.OrdinalIgnoreCase);
        }

        public static void EnableAutoStart()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.SetValue(AppName, AppPath, RegistryValueKind.String);
        }

        public static void DisableAutoStart()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.DeleteValue(AppName, false);
        }

        public static void ToggleAutoStart()
        {
            if (IsAutoStartEnabled())
                DisableAutoStart();
            else
                EnableAutoStart();
        }

        public static string GetAutoStartStatus()
        {
            return IsAutoStartEnabled() ? "Включен" : "Отключен";
        }
    }
}