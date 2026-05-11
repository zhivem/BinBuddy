using System.Diagnostics;
using System.NativeTray;
using BinBuddy.src.BinBuddy.Services;

namespace BinBuddy.src.BinBuddy
{
    internal static class Program
    {
        private static TrayIconHost? _trayIcon;
        private static AppSettings _settings = null!;
        private static System.Windows.Forms.Timer? _timer;
        private static bool _previousRecycleBinState;
        
        // Services
        private static SettingsService _settingsService = null!;
        private static RecycleBinService _recycleBinService = null!;
        private static IconPackService _iconPackService = null!;
        private static UpdateCheckService _updateCheckService = null!;
        private static AutoStartService _autoStartService = null!;
        private static RecycleBinVisibilityService _recycleBinVisibilityService = null!;

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // Initialize services
            _settingsService = new SettingsService();
            _recycleBinService = new RecycleBinService();
            _iconPackService = new IconPackService(_settingsService);
            _updateCheckService = new UpdateCheckService();
            _autoStartService = new AutoStartService();
            _recycleBinVisibilityService = new RecycleBinVisibilityService();

            _settings = _settingsService.LoadSettings();
            _previousRecycleBinState = _recycleBinService.IsEmpty();

            InitializeTrayIcon();
            InitializeTimer();
            ApplyInitialSettings();

            _ = CheckForUpdatesAsync();

            Application.Run();
        }

        private static void InitializeTrayIcon()
        {
            _trayIcon = new TrayIconHost
            {
                ToolTipText = "BinBuddy",
                Icon = LoadTrayIcon(),
                ThemeMode = TrayThemeMode.System,
                Menu = CreateContextMenu()
            };

            _trayIcon.LeftDoubleClick += (_, _) => OpenRecycleBin();
        }

        private static void InitializeTimer()
        {
            _timer = new System.Windows.Forms.Timer { Interval = _settings.UpdateIntervalSeconds * 1000 };
            _timer.Tick += (_, _) => UpdateTrayIcon();
            _timer.Start();
        }

        private static void ApplyInitialSettings()
        {
            var currentPack = _iconPackService.LoadCurrentPack();
            _iconPackService.ApplyIconPack(currentPack, _trayIcon!);

            if (!_settings.ShowRecycleBinOnDesktop)
                _recycleBinVisibilityService.Hide();
        }

        private static IntPtr LoadTrayIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "BinBuddy", "app.ico");
            if (File.Exists(iconPath))
            {
                using var icon = new Icon(iconPath);
                return icon.Handle;
            }

            var defaultIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            return defaultIcon.Handle;
        }

        private static TrayMenu CreateContextMenu()
        {
            return new TrayMenu
            {
                CreateVersionMenuItem(),
                new TraySeparator(),
                CreateMenuItem("Открыть корзину", _ => OpenRecycleBin()),
                CreateMenuItem("Очистить корзину", _ =>
                {
                    EmptyRecycleBin();
                    UpdateTrayIcon();
                }),
                new TraySeparator(),
                CreateMenuItem("Настройки", null, CreateSettingsSubmenu()),
                new TraySeparator(),
                CreateMenuItem("Выход", _ => ExitApplication(), icon: CreateExitIconFromBase64())
            };
        }

        private static TrayMenuItem CreateMenuItem(string header, Action<object?>? command = null, TrayMenu? subMenu = null, Win32Image? icon = null)
        {
            return new TrayMenuItem
            {
                Header = header,
                Command = command != null ? new TrayCommand(command) : null,
                Menu = subMenu,
                Icon = icon,
                IsEnabled = command != null || subMenu != null
            };
        }

        private static TrayMenuItem CreateVersionMenuItem()
        {
            var item = new TrayMenuItem
            {
                Header = $"BinBuddy {GetVersion()}",
                IsEnabled = false
            };
            return item;
        }

        private static Win32Image? CreateExitIconFromBase64()
        {
            try
            {
                string base64Png = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAk0lEQVR4nO3STQ4BQRCG4SfBMRB3sHKQyXAG5xJu4RRY+LsFVmSSEjLpaWNn4d1W+q2u+opfY4Jupj5GLyfYYYVOojbFLSSNjHDGsiYpcQ3JR4Y4YhGSIh5XktYMcMAal5B8zRz3kKR2kqWIzpVk/zZOK8qY+fntfoxTX2ySWURVJnZyiohzd2ITkqZ0tnFsf7x4AMKtG5ek/9cNAAAAAElFTkSuQmCC";
                byte[] imageBytes = Convert.FromBase64String(base64Png);
                using var ms = new MemoryStream(imageBytes);
                return new Win32Image(ms) { ShowAsMonochrome = true };
            }
            catch
            {
                return null;
            }
        }

        private static TrayMenu CreateSettingsSubmenu()
        {
            return new TrayMenu
            {
                CreateCheckedMenuItem("Показывать уведомления", _settings.ShowNotifications, ToggleNotifications),
                CreateCheckedMenuItem("Автозапуск", _autoStartService.IsEnabled(), ToggleAutoStart),
                CreateCheckedMenuItem("Отображать 🗑️ на рабочем столе", _settings.ShowRecycleBinOnDesktop, ToggleRecycleBinVisibility),
                new TraySeparator(),
                CreateMenuItem("Таймер обновления", null, CreateUpdateIntervalSubmenu()),
                CreateMenuItem("Выбрать иконку", null, CreateIconPackSubmenu())
            };
        }

        private static TrayMenuItem CreateCheckedMenuItem(string header, bool isChecked, Action<object?> command)
        {
            return new TrayMenuItem
            {
                Header = header,
                IsChecked = isChecked,
                Command = new TrayCommand(command)
            };
        }

        private static TrayMenu CreateUpdateIntervalSubmenu()
        {
            var subMenu = new TrayMenu();
            int[] intervals = [1, 3, 5];

            foreach (var interval in intervals)
            {
                var item = new TrayMenuItem
                {
                    Header = $"{interval} секунд",
                    IsChecked = _settings.UpdateIntervalSeconds == interval,
                    Command = new TrayCommand(_ => SetUpdateInterval(interval, subMenu))
                };
                subMenu.Add(item);
            }

            return subMenu;
        }

        private static void SetUpdateInterval(int interval, TrayMenu subMenu)
        {
            _settings.UpdateIntervalSeconds = interval;
            _settingsService.SaveSettings(_settings);

            foreach (var item in subMenu.OfType<TrayMenuItem>())
                item.IsChecked = item.Header == $"{interval} секунд";

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Interval = interval * 1000;
                _timer.Start();
            }

            ShowNotification("Обновление корзины", $"Интервал обновления установлен на {interval} секунд.");
        }

        private static TrayMenu CreateIconPackSubmenu()
        {
            var subMenu = new TrayMenu();
            string iconDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");

            Directory.CreateDirectory(iconDirectory);

            var iconPacks = Directory.GetDirectories(iconDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            string currentPack = _iconPackService.LoadCurrentPack();

            if (iconPacks.Length == 0)
            {
                subMenu.Add(new TrayMenuItem { Header = "Нет наборов иконок", IsEnabled = false });
                return subMenu;
            }

            foreach (var packName in iconPacks)
            {
                var item = new TrayMenuItem
                {
                    Header = packName!,
                    IsChecked = currentPack == packName,
                    Command = new TrayCommand(_ => ApplyIconPack(packName!, subMenu)),
                    Icon = LoadPackPreviewIcon(packName!)
                };
                subMenu.Add(item);
            }

            return subMenu;
        }

        private static void ApplyIconPack(string packName, TrayMenu subMenu)
        {
            if (_trayIcon == null) return;

            _iconPackService.ApplyIconPack(packName, _trayIcon);
            bool isRecycleBinEmpty = _recycleBinService.IsEmpty();
            _iconPackService.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);

            foreach (var item in subMenu.OfType<TrayMenuItem>())
                item.IsChecked = item.Header == packName;

            ShowNotification("Иконки", $"Набор иконок '{packName}' применен.");
        }

        private static Win32Image? LoadPackPreviewIcon(string packName)
        {
            try
            {
                string fullIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", packName, "recycle-full.ico");
                if (!File.Exists(fullIconPath)) return null;

                using var icon = new Icon(fullIconPath);
                using var originalBitmap = icon.ToBitmap();

                var resizedBitmap = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(resizedBitmap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalBitmap, 0, 0, 16, 16);
                }

                using var ms = new MemoryStream();
                resizedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                return new Win32Image(ms) { ShowAsMonochrome = false };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки иконки для {packName}: {ex.Message}");
                return null;
            }
        }

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                bool hasUpdate = await _updateCheckService.IsUpdateAvailableAsync();
                string currentVersion = GetVersion();
                string? latestVersion = _updateCheckService.GetLatestVersion();

                if (_trayIcon?.Menu is TrayMenu menu && menu[0] is TrayMenuItem versionItem)
                {
                    if (hasUpdate && !string.IsNullOrEmpty(latestVersion))
                    {
                        versionItem.Header = $"Версия {latestVersion} доступна! (нажмите для загрузки)";
                        versionItem.IsEnabled = true;
                        versionItem.Command = new TrayCommand(_ => _updateCheckService.OpenReleasesPage());

                        ShowNotification("Доступно обновление!", $"Версия {latestVersion} уже доступна для скачивания.", 5000);
                    }
                    else
                    {
                        versionItem.Header = $"BinBuddy {currentVersion}";
                        versionItem.IsEnabled = false;
                        versionItem.Command = null;
                    }
                }
            }
            catch
            {
                if (_trayIcon?.Menu is TrayMenu menu && menu[0] is TrayMenuItem versionItem)
                {
                    versionItem.Header = $"BinBuddy {GetVersion()}";
                    versionItem.IsEnabled = false;
                }
            }
        }

        private static void ToggleNotifications(object? _)
        {
            _settings.ShowNotifications = !_settings.ShowNotifications;
            _settingsService.SaveSettings(_settings);

            UpdateMenuItemCheckState("Показывать уведомления", _settings.ShowNotifications);
            ShowNotification("Уведомления", _settings.ShowNotifications ? "Уведомления включены." : "Уведомления отключены.");
        }

        private static void ToggleAutoStart(object? _)
        {
            bool enabled = !_autoStartService.IsEnabled(); 

            if (enabled)
                _autoStartService.Enable(); 
            else
                _autoStartService.Disable(); 

            _settings.AutoStartEnabled = enabled;
            _settingsService.SaveSettings(_settings);

            UpdateMenuItemCheckState("Автозапуск", enabled);
            ShowNotification("Автозапуск", enabled ? "Автозапуск включен." : "Автозапуск отключен.");
        }

        private static void ToggleRecycleBinVisibility(object? _)
        {
            _settings.ShowRecycleBinOnDesktop = !_settings.ShowRecycleBinOnDesktop;
            _settingsService.SaveSettings(_settings);

            _recycleBinVisibilityService.SetVisibility(_settings.ShowRecycleBinOnDesktop);
            UpdateMenuItemCheckState("Отображать 🗑️ на рабочем столе", _settings.ShowRecycleBinOnDesktop);
        }

        private static void UpdateMenuItemCheckState(string header, bool isChecked)
        {
            if (_trayIcon?.Menu == null) return;

            foreach (var item in GetAllMenuItems(_trayIcon.Menu))
            {
                if (item.Header == header)
                {
                    item.IsChecked = isChecked;
                    return;
                }
            }
        }

        private static IEnumerable<TrayMenuItem> GetAllMenuItems(TrayMenu menu)
        {
            foreach (var item in menu)
            {
                if (item is TrayMenuItem menuItem)
                {
                    yield return menuItem;
                    if (menuItem.Menu != null)
                    {
                        foreach (var subItem in GetAllMenuItems(menuItem.Menu))
                            yield return subItem;
                    }
                }
            }
        }

        private static void UpdateTrayIcon()
        {
            if (_trayIcon == null) return;

            bool isRecycleBinEmpty = _recycleBinService.IsEmpty();

            if (isRecycleBinEmpty != _previousRecycleBinState)
            {
                _previousRecycleBinState = isRecycleBinEmpty;
                _iconPackService.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);
            }

            UpdateTrayText();
        }

        private static void UpdateTrayText()
        {
            var rbInfo = _recycleBinService.GetRecycleBinInfo();
            _trayIcon!.ToolTipText = $"Менеджер Корзины\nЭлементов: {rbInfo.ItemCount}\nЗанято: {RecycleBinService.FormatFileSize(rbInfo.SizeInBytes)}";
        }

        private static void OpenRecycleBin()
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true });
        }

        private static void EmptyRecycleBin()
        {
            if (_recycleBinService.Empty())
            {
                ShowNotification("Корзина", "Корзина успешно очищена.");
                UpdateTrayIcon();
            }
            else
            {
                ShowNotification("Ошибка", "Не удалось очистить корзину.", 3000, TrayToolTipIcon.Error);
            }
        }

        private static void ShowNotification(string title, string message, int timeout = 3000, TrayToolTipIcon iconType = TrayToolTipIcon.Info)
        {
            if (_settings.ShowNotifications && _trayIcon != null)
                _trayIcon.ShowBalloonTip(timeout, title, message, iconType);
        }

        private static void ExitApplication()
        {
            _iconPackService?.Dispose();
            _updateCheckService?.Dispose();
            _timer?.Stop();
            _timer?.Dispose();
            _trayIcon?.Dispose();
            Application.Exit();
        }

        private static string GetVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0";
        }
    }
}