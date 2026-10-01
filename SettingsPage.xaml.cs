using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZapretReborn
{
    public partial class SettingsPage : Page
    {
        private readonly string _appPath = Process.GetCurrentProcess().MainModule.FileName;
        private string _dynamicZapretFolderPath = string.Empty;
        private bool _isCheckingUpdates = false;

        public SettingsPage()
        {
            InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeThemeSettings();
            InitializeAutostartSettings();
            InitializeVersionInfo();
            SettingsUpdateBadge.Visibility = App.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

            // Если есть доступные обновления, обновляем информацию о версиях
            if (App.IsUpdateAvailable)
            {
                UpdateVersionsDisplay();
            }
        }

        private void InitializeThemeSettings()
        {
            try
            {
                // Проверяем, есть ли сохраненная тема в реестре
                int savedThemeIndex = GetSavedThemeFromRegistry();
                if (savedThemeIndex >= 0)
                {
                    ThemeComboBox.SelectedIndex = savedThemeIndex;
                    
                    // Применяем сохраненную тему ко всей странице
                    ElementTheme selectedTheme = savedThemeIndex switch
                    {
                        0 => ElementTheme.Default,
                        1 => ElementTheme.Light,
                        2 => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                    
                    this.RequestedTheme = selectedTheme;
                    return;
                }
                
                // Если нет сохраненной темы, используем текущую
                if (this.Frame?.XamlRoot?.Content is FrameworkElement rootElement)
                {
                    ThemeComboBox.SelectedIndex = rootElement.RequestedTheme switch
                    {
                        ElementTheme.Default => 0,
                        ElementTheme.Light => 1,
                        ElementTheme.Dark => 2,
                        _ => 0
                    };
                }
                else
                {
                    // Фоллбек на системную тему
                    ThemeComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации темы: {ex.Message}");
                ThemeComboBox.SelectedIndex = 0;
            }
        }
        
        private int GetSavedThemeFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\ZapretReborn", writable: false);
                if (key != null)
                {
                    var value = key.GetValue("Theme");
                    if (value != null && int.TryParse(value.ToString(), out int index))
                    {
                        return index;
                    }
                }
            }
            catch { }
            return -1;
        }
        
        private void SaveThemeToRegistry(int themeIndex)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\ZapretReborn", writable: true);
                if (key != null)
                {
                    key.SetValue("Theme", themeIndex.ToString());
                }
            }
            catch { }
        }

        private void InitializeAutostartSettings()
        {
            AutostartCheckBox.IsChecked = CheckAutostartExists();
        }

        private void InitializeVersionInfo()
        {
            try
            {
                // Инициализируем текущие версии
                if (UiVersionRun != null)
                {
                    UiVersionRun.Text = $"v{GetCurrentUiVersion()}";
                }
                LoadScriptVersion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации информации о версиях: {ex.Message}");
            }
        }

        private string GetCurrentUiVersion()
        {
            return UpdateResult.GetCurrentUiVersion();
        }

        private void LoadScriptVersion()
        {
            try
            {
                if (ZapretVersionRun == null) return;
                
                string appDirectory = AppPaths.ApplicationDirectory;
                
                // Ищем папки с разными шаблонами
                var patterns = new[] { "zapret-*", "zapret-discord-*" };
                string[] matchingDirectories = Array.Empty<string>();
                
                foreach (var pattern in patterns)
                {
                    matchingDirectories = Directory.GetDirectories(appDirectory, pattern);
                    if (matchingDirectories.Length > 0)
                    {
                        break;
                    }
                }
                
                if (matchingDirectories.Length == 0)
                {
                    ZapretVersionRun.Text = "папка не найдена";
                    return;
                }

                _dynamicZapretFolderPath = matchingDirectories[0];

                // Попытка прочитать из service.bat (для совместимости)
                string serviceFilePath = Path.Combine(_dynamicZapretFolderPath, "service.bat");
                if (File.Exists(serviceFilePath))
                {
                    foreach (var line in File.ReadAllLines(serviceFilePath))
                    {
                        var t = line.Trim();
                        // Поддерживаем разные форматы: set "LOCAL_VERSION=..., set LOCAL_VERSION=..., LOCAL_VERSION=...
                        if (t.StartsWith("set \"LOCAL_VERSION=") ||
                            t.StartsWith("set LOCAL_VERSION=") ||
                            t.StartsWith("LOCAL_VERSION="))
                        {
                            var parts = t.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var version = parts[1].Trim().Trim('"', '\'', ' ');
                                ZapretVersionRun.Text = $"v{version}";
                                return;
                            }
                        }
                    }
                }

                // Ищем версию в любом .bat файле
                var batFiles = Directory.GetFiles(_dynamicZapretFolderPath, "*.bat", SearchOption.AllDirectories);
                foreach (var batFile in batFiles)
                {
                    foreach (var line in File.ReadAllLines(batFile))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("set \"LOCAL_VERSION=") ||
                            t.StartsWith("set LOCAL_VERSION=") ||
                            t.StartsWith("LOCAL_VERSION="))
                        {
                            var parts = t.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var version = parts[1].Trim().Trim('"', '\'', ' ');
                                ZapretVersionRun.Text = $"v{version}";
                                return;
                            }
                        }
                    }
                }

                // Резерв: парсим имя папки (например, zapret-discord-youtube-1.10.3)
                var folderName = Path.GetFileName(_dynamicZapretFolderPath) ?? string.Empty;
                var m = Regex.Match(folderName, @"\d+\.\d+\.\d+[a-z]?", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    ZapretVersionRun.Text = $"v{m.Value}";
                    return;
                }

                // Последний резерв: используем константу
                ZapretVersionRun.Text = $"v{AppConstants.CurrentScriptVersion}";
            }
            catch
            {
                if (ZapretVersionRun != null)
                {
                    ZapretVersionRun.Text = "ошибка чтения";
                }
            }
        }

        /// <summary>
        /// Обновляет отображение версий после проверки обновлений
        /// </summary>
        private async void UpdateVersionsDisplay()
        {
            try
            {
                // Проверяем, что элементы UI существуют
                if (UiVersionRun != null)
                {
                    UiVersionRun.Text = $"v{GetCurrentUiVersion()}";
                }

                // Проверяем, есть ли папка Zapret
                if (AppPaths.ZapretFolder != null && ZapretVersionRun != null)
                {
                    LoadScriptVersion();
                }
                else if (ZapretVersionRun != null)
                {
                    ZapretVersionRun.Text = "папка не найдена";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления версий: {ex.Message}");
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox == null) return;

            try
            {
                ElementTheme selectedTheme = ThemeComboBox.SelectedIndex switch
                {
                    0 => ElementTheme.Default,
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                // Сохраняем выбранную тему в реестре
                SaveThemeToRegistry(ThemeComboBox.SelectedIndex);

                // Применяем тему к текущей странице
                this.RequestedTheme = selectedTheme;
                
                // Также применяем к корневому элементу, если он есть
                if (this.Frame?.XamlRoot?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = selectedTheme;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка изменения темы: {ex.Message}");
            }
        }

        private void AutostartCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (AutostartCheckBox == null) return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                if (key == null) return;

                if (AutostartCheckBox.IsChecked == true)
                {
                    key.SetValue(AppConstants.AppName, $"\"{_appPath}\"");
                }
                else
                {
                    if (key.GetValue(AppConstants.AppName) != null)
                    {
                        key.DeleteValue(AppConstants.AppName, throwOnMissingValue: false);
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки доступа к реестру
            }
        }

        private bool CheckAutostartExists()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
                return key?.GetValue(AppConstants.AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверка и установка обновлений UI и скриптов
        /// </summary>
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCheckingUpdates) return;
            
            // Проверяем, что все элементы UI существуют
            if (CheckUpdatesButton == null || UpdateProgressBar == null || UpdateStatusText == null)
            {
                System.Diagnostics.Debug.WriteLine("CheckUpdatesButton_Click: UI элементы не инициализированы");
                return;
            }

            _isCheckingUpdates = true;
            CheckUpdatesButton.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            UpdateStatusText.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "Проверка обновлений...";
            UpdateStatusText.Foreground = new SolidColorBrush(Colors.Gray);

            try
            {
                bool uiUpdated = false;
                bool scriptUpdated = false;
                string? installedUiVersion = null;
                string? installedScriptVersion = null;

                // 1. Проверяем и устанавливаем обновление UI
                UpdateStatusText.Text = "Проверка обновлений UI...";
                var uiUpdateResult = await UpdateService.CheckUiUpdatesAsync();

                if (uiUpdateResult.HasUpdate)
                {
                    UpdateStatusText.Text = $"Найдено обновление UI: v{uiUpdateResult.LatestVersion}";

                    if (!string.IsNullOrEmpty(uiUpdateResult.DownloadUrl))
                    {
                        UpdateStatusText.Text = "Скачивание обновления UI...";
                        var downloadProgress = new Progress<double>(percent =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateProgressBar.Value = percent;
                            });
                        });

                        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(uiUpdateResult.DownloadUrl));
                        bool downloaded = await UpdateService.DownloadUpdateAsync(uiUpdateResult.DownloadUrl, tempFile, downloadProgress);

                        if (downloaded)
                        {
                            var installProgress = new Progress<string>(status =>
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    UpdateStatusText.Text = status;
                                });
                            });

                            bool installed = await UpdateService.InstallUiUpdateAsync(tempFile, installProgress);

                            if (installed)
                            {
                                uiUpdated = true;
                                installedUiVersion = uiUpdateResult.LatestVersion;
                                UpdateStatusText.Text = "UI-обновление подготовлено к установке после закрытия приложения";
                                UpdateStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                UiVersionRun.Text = $"v{uiUpdateResult.LatestVersion}";
                            }
                            else
                            {
                                UpdateStatusText.Text = "Ошибка установки UI обновления";
                                UpdateStatusText.Foreground = new SolidColorBrush(Colors.Red);
                            }
                        }
                        else
                        {
                            UpdateStatusText.Text = "Ошибка скачивания UI обновления";
                            UpdateStatusText.Foreground = new SolidColorBrush(Colors.Red);
                        }
                    }
                    else
                    {
                        UpdateStatusText.Text = "Обновление UI найдено, но URL для скачивания отсутствует";
                        UpdateStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                }
                else
                {
                    UpdateStatusText.Text = "Обновления UI не найдены";
                }

                // 2. Проверяем и устанавливаем обновление скриптов
                UpdateStatusText.Text = "Проверка обновлений скриптов...";
                var scriptUpdateResult = await UpdateService.CheckScriptUpdatesAsync();

                if (scriptUpdateResult.HasUpdate)
                {
                    UpdateStatusText.Text = $"Найдено обновление скриптов: v{scriptUpdateResult.LatestVersion}";

                    var installProgress = new Progress<string>(status =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateStatusText.Text = status;
                        });
                    });

                    bool installed = await UpdateService.InstallScriptUpdateAsync(installProgress);

                    if (installed)
                    {
                        scriptUpdated = true;
                        installedScriptVersion = scriptUpdateResult.LatestVersion;
                        UpdateStatusText.Text = "Скрипты успешно обновлены!";
                        UpdateStatusText.Foreground = new SolidColorBrush(Colors.Green);

                        // Обновляем отображение версии скриптов
                        LoadScriptVersion();
                    }
                    else
                    {
                        UpdateStatusText.Text = "Ошибка обновления скриптов";
                        UpdateStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
                else
                {
                    UpdateStatusText.Text = scriptUpdated ? "Скрипты обновлены" : "Обновления скриптов не найдены";
                }

                // Итоговый статус
                if (uiUpdated || scriptUpdated)
                {
                    UpdateStatusText.Text = uiUpdated
                        ? "UI-обновление подготовлено; закройте приложение для установки."
                        : "Обновления установлены!";
                    UpdateStatusText.Foreground = new SolidColorBrush(Colors.Green);

                }
                else if (!uiUpdated && !scriptUpdated)
                {
                    UpdateStatusText.Text = "Все обновления установлены";
                    UpdateStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                }

                // Обновляем информацию о версиях
                UpdateVersionsDisplay();

                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.SetUpdateAvailability(
                        uiUpdateResult.HasUpdate && !uiUpdated,
                        scriptUpdateResult.HasUpdate && !scriptUpdated,
                        installedUiVersion,
                        installedScriptVersion);
                }
                SettingsUpdateBadge.Visibility = App.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

                if (uiUpdated)
                {
                    await ShowRestartRequiredDialog();
                }

            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Ошибка: {ex.Message}";
                UpdateStatusText.Foreground = new SolidColorBrush(Colors.Red);
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
            finally
            {
                _isCheckingUpdates = false;
                CheckUpdatesButton.IsEnabled = true;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task ShowRestartRequiredDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Обновление подготовлено",
                Content = "Single-file обновление скачано. Чтобы применить его, закройте приложение. Установщик заменит EXE и запустит новую версию.",
                PrimaryButtonText = "Закрыть и обновить",
                CloseButtonText = "Позже",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                RestartApplication();
            }
        }

        private void RestartApplication()
        {
            try
            {
                App.MainWindow.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка перезапуска: {ex.Message}");
            }
        }
    }
}