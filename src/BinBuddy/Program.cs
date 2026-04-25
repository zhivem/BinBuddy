using System.Diagnostics;
using System.NativeTray;
using System.Runtime.InteropServices;

namespace BinBuddy.src.BinBuddy
{
    internal static class Program
    {
        private static TrayIconHost? _trayIcon;
        private static bool _showNotifications = true;
        private static bool _showRecycleBinOnDesktop = true;
        private static bool _previousRecycleBinState = true;
        private static System.Windows.Forms.Timer? _timer;
        private static AppSettings _settings = null!;

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            _settings = SettingsManager.LoadSettings();
            _showNotifications = _settings.ShowNotifications;
            _showRecycleBinOnDesktop = _settings.ShowRecycleBinOnDesktop;
            int updateInterval = _settings.UpdateIntervalSeconds;

            IntPtr iconHandle = LoadTrayIcon();

            _trayIcon = new TrayIconHost()
            {
                ToolTipText = "BinBuddy",
                Icon = iconHandle,
                ThemeMode = TrayThemeMode.System,
                Menu = CreateContextMenu()
            };

            // Событие двойного клика
            _trayIcon.LeftDoubleClick += (_, _) => OpenRecycleBin();

            _timer = new System.Windows.Forms.Timer { Interval = updateInterval * 1000 };
            _timer.Tick += (_, _) => UpdateTrayIcon();
            _timer.Start();

            var currentPack = IconPackManager.LoadCurrentPack();
            IconPackManager.ApplyIconPack(currentPack, _trayIcon);

            // Запускаем проверку обновлений асинхронно
            _ = CheckForUpdatesAsync();

            Application.Run();
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
            string versionText = GetVersionText();

            return new TrayMenu
            {
                new TrayMenuItem
                {
                    Header = versionText,
                    IsEnabled = false  // По умолчанию некликабельный
                },
                new TraySeparator(),
                new TrayMenuItem
                {
                    Header = "Открыть корзину",
                    Command = new TrayCommand(_ => OpenRecycleBin())
                },
                new TrayMenuItem
                {
                    Header = "Очистить корзину",
                    Command = new TrayCommand(_ =>
                    {
                        EmptyRecycleBin();
                        UpdateTrayIcon();
                    })
                },
                new TraySeparator(),
                new TrayMenuItem
                {
                    Header = "Настройки",
                    Menu = CreateSettingsSubmenu()
                },
                new TraySeparator(),
                new TrayMenuItem
                {
                    Header = "Выход",
                    Command = new TrayCommand(_ => ExitApplication())
                }
            };
        }

        private static string GetVersionText()
        {
            string currentVersion = GetVersion();
            return $"BinBuddy {currentVersion} (проверка обновлений...)";
        }

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                bool hasUpdate = await UpdateChecker.IsUpdateAvailableAsync();
                string currentVersion = GetVersion();
                string? latestVersion = UpdateChecker.GetLatestVersion();

                if (_trayIcon?.Menu is TrayMenu menu && menu.Count > 0)
                {
                    var versionItem = menu[0] as TrayMenuItem;
                    if (versionItem != null)
                    {
                        if (hasUpdate && !string.IsNullOrEmpty(latestVersion))
                        {
                            versionItem.Header = $"Версия {latestVersion} доступна! (нажмите для загрузки)";
                            versionItem.IsEnabled = true;
                            versionItem.Command = new TrayCommand(_ => UpdateChecker.OpenReleasesPage());

                            // Показываем уведомление о наличии обновления
                            ShowBalloonNotification(
                                "Доступно обновление!",
                                $"Версия {latestVersion} уже доступна для скачивания.",
                                TrayToolTipIcon.Info,
                                5000
                            );
                        }
                        else
                        {
                            versionItem.Header = $"BinBuddy {currentVersion}";
                            versionItem.IsEnabled = false;
                            versionItem.Command = null;
                        }
                    }
                }
            }
            catch
            {
                // Если проверка не удалась, оставляем стандартную надпись
                if (_trayIcon?.Menu is TrayMenu menu && menu.Count > 0)
                {
                    var versionItem = menu[0] as TrayMenuItem;
                    if (versionItem != null)
                    {
                        versionItem.Header = $"BinBuddy {GetVersion()}";
                        versionItem.IsEnabled = false;
                    }
                }
            }
        }

        private static TrayMenu CreateSettingsSubmenu()
        {
            return new TrayMenu
        {
            new TrayMenuItem
            {
                Header = "Показывать уведомления",
                IsChecked = _showNotifications,
                Command = new TrayCommand(_ => ToggleNotifications())
            },
            new TrayMenuItem
            {
                Header = "Автозапуск",
                IsChecked = AutoStartManager.IsAutoStartEnabled(),
                Command = new TrayCommand(_ => ToggleAutoStart())
            },
            new TrayMenuItem
            {
                Header = "Отображать 🗑️ на рабочем столе",
                IsChecked = _showRecycleBinOnDesktop,
                Command = new TrayCommand(_ => ToggleRecycleBinVisibility())
            },
            new TraySeparator(),
            new TrayMenuItem
            {
                Header = "Таймер обновления",
                Menu = CreateUpdateIntervalSubmenu()
            },
            new TrayMenuItem
            {
                Header = "Выбрать иконку",
                Menu = CreateIconPackSubmenu()
            }
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
                    IsChecked = _settings.UpdateIntervalSeconds == interval
                };

                var capturedInterval = interval;
                item.Command = new TrayCommand(_ =>
                {
                    _settings.UpdateIntervalSeconds = capturedInterval;
                    SettingsManager.SaveSettings(_settings);

                    foreach (var menuItem in subMenu)
                    {
                        if (menuItem is TrayMenuItem mi)
                            mi.IsChecked = mi.Header == $"{capturedInterval} секунд";
                    }

                    _timer?.Stop();
                    if (_timer != null)
                    {
                        _timer.Interval = capturedInterval * 1000;
                        _timer.Start();
                    }

                    ShowBalloonNotification(
                        "Обновление корзины",
                        $"Интервал обновления установлен на {capturedInterval} секунд.",
                        TrayToolTipIcon.Info
                    );
                });

                subMenu.Add(item);
            }

            return subMenu;
        }

        private static TrayMenu CreateIconPackSubmenu()
        {
            var subMenu = new TrayMenu();
            string iconDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");

            if (!Directory.Exists(iconDirectory))
            {
                Directory.CreateDirectory(iconDirectory);
            }

            var iconPacks = Directory.GetDirectories(iconDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            string currentPack = IconPackManager.LoadCurrentPack();

            foreach (var packName in iconPacks)
            {
                var item = new TrayMenuItem
                {
                    Header = packName!,
                    IsChecked = (currentPack == packName),
                    Command = new TrayCommand(_ =>
                    {
                        if (_trayIcon != null)
                        {
                            IconPackManager.ApplyIconPack(packName!, _trayIcon);
                            bool isRecycleBinEmpty = IsRecycleBinEmpty();
                            IconPackManager.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);

                            UpdateIconPackCheckmarks(subMenu, packName!);

                            ShowBalloonNotification(
                                "Иконки",
                                $"Набор иконок '{packName}' применен.",
                                TrayToolTipIcon.Info
                            );
                        }
                    })
                };
                subMenu.Add(item);
            }

            if (subMenu.Count == 0)
            {
                subMenu.Add(new TrayMenuItem
                {
                    Header = "Нет наборов иконок",
                    IsEnabled = false
                });
            }

            return subMenu;
        }

        private static void UpdateIconPackCheckmarks(TrayMenu subMenu, string selectedPack)
        {
            foreach (var item in subMenu)
            {
                if (item is TrayMenuItem menuItem && menuItem.Header != "Нет наборов иконок")
                {
                    menuItem.IsChecked = (menuItem.Header == selectedPack);
                }
            }
        }

        private static string GetVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString(3) ?? "1.0";
        }

        private static void ToggleNotifications()
        {
            _showNotifications = !_showNotifications;
            _settings.ShowNotifications = _showNotifications;
            SettingsManager.SaveSettings(_settings);

            UpdateMenuItemCheckState("Показывать уведомления", _showNotifications);

            ShowBalloonNotification(
                "Уведомления",
                _showNotifications ? "Уведомления включены." : "Уведомления отключены.",
                TrayToolTipIcon.Info
            );
        }

        private static void ToggleAutoStart()
        {
            bool wasAutoStartEnabled = AutoStartManager.IsAutoStartEnabled();

            if (wasAutoStartEnabled)
            {
                AutoStartManager.DisableAutoStart();
                _settings.AutoStartEnabled = false;
                ShowBalloonNotification("Автозапуск", "Автозапуск отключен.", TrayToolTipIcon.Info);
            }
            else
            {
                AutoStartManager.EnableAutoStart();
                _settings.AutoStartEnabled = true;
                ShowBalloonNotification("Автозапуск", "Автозапуск включен.", TrayToolTipIcon.Info);
            }

            SettingsManager.SaveSettings(_settings);
            UpdateMenuItemCheckState("Автозапуск", AutoStartManager.IsAutoStartEnabled());
        }

        private static void ToggleRecycleBinVisibility()
        {
            _showRecycleBinOnDesktop = !_showRecycleBinOnDesktop;
            _settings.ShowRecycleBinOnDesktop = _showRecycleBinOnDesktop;
            SettingsManager.SaveSettings(_settings);

            RecycleBinVisibilityManager.SetRecycleBinVisibilityOnDesktop(_showRecycleBinOnDesktop);
            UpdateMenuItemCheckState("Отображать 🗑️ на рабочем столе", _showRecycleBinOnDesktop);
        }

        private static void UpdateMenuItemCheckState(string header, bool isChecked)
        {
            if (_trayIcon?.Menu is TrayMenu menu)
            {
                foreach (var item in menu)
                {
                    if (item is TrayMenuItem menuItem && menuItem.Header == header)
                    {
                        menuItem.IsChecked = isChecked;
                        break;
                    }

                    // Проверяем вложенные меню
                    if (item is TrayMenuItem subMenuItem && subMenuItem.Menu != null)
                    {
                        foreach (var subItem in subMenuItem.Menu)
                        {
                            if (subItem is TrayMenuItem targetItem && targetItem.Header == header)
                            {
                                targetItem.IsChecked = isChecked;
                                return;
                            }
                        }
                    }
                }
            }
        }

        private static void UpdateTrayIcon()
        {
            if (_trayIcon == null) return;

            bool isRecycleBinEmpty = IsRecycleBinEmpty();

            if (isRecycleBinEmpty != _previousRecycleBinState)
            {
                _previousRecycleBinState = isRecycleBinEmpty;
                IconPackManager.UpdateIconsBasedOnState(_trayIcon, isRecycleBinEmpty);
            }

            UpdateTrayText();
        }

        private static bool IsRecycleBinEmpty()
        {
            var rbInfo = new SHQUERYRBINFO
            {
                cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>()
            };
            SHQueryRecycleBin(null, ref rbInfo);
            return rbInfo.i64NumItems == 0;
        }

        private static void UpdateTrayText()
        {
            var rbInfo = new SHQUERYRBINFO
            {
                cbSize = (uint)Marshal.SizeOf<SHQUERYRBINFO>()
            };
            SHQueryRecycleBin(null, ref rbInfo);

            string text = $"Менеджер Корзины\nЭлементов: {rbInfo.i64NumItems}\nЗанято: {FormatFileSize(rbInfo.i64Size)}";

            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = text;
            }
        }

        private static string FormatFileSize(long sizeInBytes)
        {
            string[] sizes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
            double len = sizeInBytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private static void OpenRecycleBin()
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder")
            {
                UseShellExecute = true
            });
        }

        private static void EmptyRecycleBin()
        {
            const uint SHERB_NOCONFIRMATION = 0x00000001;
            const uint SHERB_NOPROGRESSUI = 0x00000002;
            const uint SHERB_NOSOUND = 0x00000004;

            int result = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

            if (result == 0)
            {
                ShowBalloonNotification("Корзина", "Корзина успешно очищена.", TrayToolTipIcon.Info);
                UpdateTrayIcon();
            }
            else
            {
                ShowBalloonNotification("Ошибка", "Не удалось очистить корзину.", TrayToolTipIcon.Error);
            }
        }

        private static void ShowBalloonNotification(string title, string? message, TrayToolTipIcon iconType, int timeout = 3000)
        {
            if (!_showNotifications || _trayIcon == null) return;

            _trayIcon.ShowBalloonTip(timeout, title, message, iconType);
        }

        private static void ExitApplication()
        {
            IconPackManager.DisposeIcons();
            _timer?.Stop();
            _timer?.Dispose();
            _trayIcon?.Dispose();

            Application.Exit();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }
    }
}