using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ZapretReborn
{
    /// <summary>
    /// Статический класс для отслеживания состояния сервиса
    /// </summary>
    public static class ServiceStatus
    {
        public static bool IsRebooted { get; set; } = true;
    }

    /// <summary>
    /// Класс для представления скрипта
    /// </summary>
    public class ScriptItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Вспомогательный класс для анимации (используется только в HomePage)
    /// </summary>
    internal static class AnimationHelper
    {
        public static void StartAnimation(this DependencyObject obj, string path, double toValue, int durationMs, EasingFunctionBase easing = null)
        {
            var animation = new DoubleAnimation
            {
                To = toValue,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EnableDependentAnimation = true,
                EasingFunction = easing ?? new QuinticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, obj);
            Storyboard.SetTargetProperty(animation, path);

            var sb = new Storyboard();
            sb.Children.Add(animation);
            sb.Begin();
        }
    }

    public sealed partial class HomePage : Page
    {
        private Process _scriptProcess;
        private string _scriptsFolder = AppPaths.ZapretFolder;
        private bool _isScriptsLoaded = false;
        private bool _hasCheckedStatus = false;
        private readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.ConfigFileName);

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseProxy = false
        })
        {
            Timeout = AppConstants.DefaultHttpTimeout
        };

        public HomePage()
        {
            InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            LoadScripts();

            this.Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is Window mainWindow)
            {
                mainWindow.Closed -= MainWindow_Closed;
                mainWindow.Closed += MainWindow_Closed;
            }

            // Проверяем, запущена ли служба прямо сейчас
            bool isAlreadyRunning = Process.GetProcessesByName("winws").Length > 0;

            int durationMs = 950;
            var cubicEasing = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (!isAlreadyRunning && !ConnectionSwitch.IsOn)
            {
                // Каскадная анимация при чистом запуске
                HeaderPanel.StartAnimation("Opacity", 1.0, durationMs, cubicEasing);
                HeaderTransform.StartAnimation("Y", 0.0, durationMs, cubicEasing);

                await Task.Delay(950);

                MainControlCard.StartAnimation("Opacity", 1.0, durationMs, cubicEasing);
                MainControlCardTransform.StartAnimation("Y", 0.0, durationMs, cubicEasing);
            }
            else
            {
                // Мгновенное отображение если служба активна
                HeaderPanel.Opacity = 1.0;
                HeaderTransform.Y = 0.0;
                MainControlCard.Opacity = 1.0;
                MainControlCardTransform.Y = 0.0;
            }

            CheckExistingProcess();
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (ConnectionSwitch.IsOn || Process.GetProcessesByName("winws").Length > 0)
            {
                args.Handled = true;
                StatusText.Text = "Очистка системы...";
                await StopScript();
                App.MainWindow.Close();
            }
        }

        private void LoadScripts()
        {
            if (_isScriptsLoaded && ScriptComboBox.ItemsSource != null) return;
            _scriptsFolder = AppPaths.ZapretFolder;
            try
            {
                if (!Directory.Exists(_scriptsFolder))
                {
                    ScriptComboBox.PlaceholderText = "Папка не найдена";
                    return;
                }

                string[] batFiles = Directory.GetFiles(_scriptsFolder, "g*.bat");
                List<ScriptItem> items = new List<ScriptItem>();

                foreach (var file in batFiles)
                {
                    items.Add(new ScriptItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FullPath = file
                    });
                }

                ScriptComboBox.ItemsSource = items;

                if (items.Count > 0)
                {
                    string savedScriptName = null;
                    if (File.Exists(_settingsFilePath))
                    {
                        try { savedScriptName = File.ReadAllText(_settingsFilePath, Encoding.UTF8)?.Trim(); } catch { }
                    }

                    if (!string.IsNullOrEmpty(savedScriptName))
                    {
                        var savedItem = items.Find(x => x.Name.Equals(savedScriptName, StringComparison.OrdinalIgnoreCase));
                        ScriptComboBox.SelectedItem = savedItem ?? items[0];
                    }
                    else
                    {
                        ScriptComboBox.SelectedIndex = 0;
                    }
                    _isScriptsLoaded = true;
                }
                else
                {
                    ScriptComboBox.PlaceholderText = "Скрипты не найдены";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка загрузки списка: {ex.Message}";
            }
        }

        private async void ConnectionSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggle = sender as ToggleSwitch;
            if (toggle == null) return;

            int durationMs = 400;
            var sharedEasing = new QuinticEase { EasingMode = EasingMode.EaseInOut };

            if (toggle.IsOn)
            {
                await StartScript();
                if (!ConnectionSwitch.IsOn) return;

                StatusPanel.Visibility = Visibility.Visible;

                double currentFullHeight = MainControlCard.ActualHeight;
                MainControlCard.Height = currentFullHeight;

                double compactHeight = TopControlPanel.ActualHeight > 0 ? TopControlPanel.ActualHeight : 72;

                MainControlCard.StartAnimation("Height", compactHeight, durationMs, sharedEasing);
                ScriptSelectionTransform.StartAnimation("Y", -(currentFullHeight - compactHeight), durationMs, sharedEasing);
                ScriptSelectionContainer.StartAnimation("Opacity", 0.0, 200, new CubicEase { EasingMode = EasingMode.EaseIn });

                StatusPanel.StartAnimation("Opacity", 1.0, durationMs, sharedEasing);
                StatusPanelTransform.StartAnimation("Y", 0.0, durationMs, sharedEasing);

                _hasCheckedStatus = true;
                ServiceStatus.IsRebooted = true;
                _ = CheckWebsitesAvailabilityAsync();
            }
            else
            {
                ScriptSelectionContainer.Opacity = 1;

                StatusPanel.StartAnimation("Opacity", 0, 200, sharedEasing);
                StatusPanelTransform.StartAnimation("Y", 15, 200, sharedEasing);

                MainControlCard.Height = double.NaN;
                MainControlCard.Measure(new Windows.Foundation.Size(MainControlCard.ActualWidth, double.PositiveInfinity));
                double targetFullHeight = MainControlCard.DesiredSize.Height;

                double compactHeight = MainControlCard.ActualHeight;
                MainControlCard.Height = compactHeight;

                MainControlCard.StartAnimation("Height", targetFullHeight, durationMs, sharedEasing);
                ScriptSelectionTransform.StartAnimation("Y", 0.0, durationMs, sharedEasing);

                var timer = new System.Threading.Timer((obj) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!ConnectionSwitch.IsOn)
                        {
                            MainControlCard.Height = double.NaN;
                            StatusPanel.Visibility = Visibility.Collapsed;
                        }
                    });
                }, null, durationMs + 50, System.Threading.Timeout.Infinite);

                _hasCheckedStatus = false;

                await StopScript();
            }
        }

        private async Task CheckWebsitesAvailabilityAsync()
        {
            ResetStatusControls(YouTubeProgress, YouTubeStatusIcon);
            ResetStatusControls(DiscordProgress, DiscordStatusIcon);

            await Task.Delay(AppConstants.TrafficInterceptDelay);

            var youtubeTask = CheckUrlAsync("https://www.youtube.com");
            var discordTask = CheckUrlAsync("https://discord.com");

            await Task.WhenAll(youtubeTask, discordTask);

            UpdateStatusUI(youtubeTask.Result, YouTubeProgress, YouTubeStatusIcon);
            UpdateStatusUI(discordTask.Result, DiscordProgress, DiscordStatusIcon);
        }

        private void ResetStatusControls(ProgressRing ring, SymbolIcon icon)
        {
            ring.IsActive = true;
            ring.Visibility = Visibility.Visible;
            icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private void UpdateStatusUI(bool isAvailable, ProgressRing ring, SymbolIcon icon)
        {
            ring.IsActive = false;
            ring.Visibility = Visibility.Collapsed;

            if (isAvailable)
            {
                icon.Symbol = Symbol.Accept;
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                icon.Symbol = Symbol.Cancel;
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async Task<bool> CheckUrlAsync(string url)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task StartScript()
        {
            ScriptItem selectedScript = ScriptComboBox.SelectedItem as ScriptItem;
            if (selectedScript == null)
            {
                StatusText.Text = "Ошибка: Скрипт не выбран!";
                ToggleConnectionSwitch(false);
                return;
            }

            try
            {
                StatusText.Text = $"Запуск {selectedScript.Name}...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);
                ScriptComboBox.IsEnabled = false;

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"{selectedScript.FullPath}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                _scriptProcess = new Process { StartInfo = startInfo };
                _scriptProcess.Start();

                await Task.Delay(AppConstants.ScriptStartupWait);

                int checkCount = 0;
                bool processStarted = false;
                while (checkCount < 10 && !processStarted)
                {
                    await Task.Delay((int)AppConstants.ScriptCheckInterval.TotalMilliseconds);
                    processStarted = Process.GetProcessesByName("winws").Length > 0;
                    checkCount++;
                }

                if (!processStarted)
                {
                    throw new TimeoutException("Сервис winws не запустился в течение 5 секунд");
                }

                StatusText.Text = $"Работает: {selectedScript.Name}";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);

                try { File.WriteAllText(_settingsFilePath, selectedScript.Name, Encoding.UTF8); } catch { }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
                Debug.WriteLine(ex.Message);
                ToggleConnectionSwitch(false);
                ScriptComboBox.IsEnabled = true;
            }
        }

        private void ToggleConnectionSwitch(bool isOn)
        {
            ConnectionSwitch.Toggled -= ConnectionSwitch_Toggled;
            ConnectionSwitch.IsOn = isOn;
            ConnectionSwitch.Toggled += ConnectionSwitch_Toggled;
        }

        private async Task StopScript()
        {
            StatusText.Text = "Остановка службы и очистка...";
            StatusText.Foreground = new SolidColorBrush(Colors.Orange);

            try
            {
                await Task.Run(() =>
                {
                    Process[] processes = Process.GetProcessesByName("winws");
                    foreach (Process process in processes)
                    {
                        try { process.Kill(entireProcessTree: true); } catch { }
                    }

                    if (_scriptProcess != null && !_scriptProcess.HasExited)
                    {
                        try { _scriptProcess.Kill(entireProcessTree: true); } catch { }
                    }
                });

                await Task.Delay(AppConstants.CleanupDelay);

                await Task.Run(() =>
                {
                    RunSystemCommand("net stop WinDivert");
                    RunSystemCommand("sc delete WinDivert");
                    RunSystemCommand("net stop WinDivert14");
                    RunSystemCommand("sc delete WinDivert14");
                });

                StatusText.Text = "Отключено";
                StatusText.Foreground = new SolidColorBrush(Colors.Gray);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка при остановке: {ex.Message}";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
            finally
            {
                ScriptComboBox.IsEnabled = true;
            }
        }

        private void RunSystemCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command} >nul 2>&1",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi)) { p?.WaitForExit(3000); }
            }
            catch { }
        }

        private void CheckExistingProcess()
        {
            bool isRunning = Process.GetProcessesByName("winws").Length > 0;

            if (isRunning)
            {
                ToggleConnectionSwitch(true);

                ScriptSelectionContainer.Opacity = 0;
                ScriptComboBox.IsEnabled = false;

                StatusPanel.Visibility = Visibility.Visible;
                StatusPanel.Opacity = 1;
                StatusPanelTransform.Y = 0;

                DispatcherQueue.TryEnqueue(() =>
                {
                    double compactHeight = TopControlPanel.ActualHeight > 0 ? TopControlPanel.ActualHeight : 72;
                    MainControlCard.Height = compactHeight;
                });

                ScriptItem selectedScript = ScriptComboBox.SelectedItem as ScriptItem;
                StatusText.Text = selectedScript != null ? $"Работает: {selectedScript.Name}" : "Работает";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);

                if (!_hasCheckedStatus)
                {
                    _hasCheckedStatus = true;
                    _ = CheckWebsitesAvailabilityAsync();
                }
            }
            else
            {
                _hasCheckedStatus = false;

                if (ConnectionSwitch.IsOn)
                {
                    ToggleConnectionSwitch(false);

                    ScriptSelectionContainer.Opacity = 1;
                    ScriptComboBox.IsEnabled = true;
                    MainControlCard.Height = double.NaN;
                    StatusPanel.Visibility = Visibility.Collapsed;

                    StatusText.Text = "Отключено";
                    StatusText.Foreground = new SolidColorBrush(Colors.Gray);
                }
            }
        }

        public void RefreshScripts()
        {
            _isScriptsLoaded = false;
            AppPaths.ResetCache();
            LoadScripts();
        }
    }
}