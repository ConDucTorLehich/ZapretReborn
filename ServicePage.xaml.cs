using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ZapretReborn
{
    public partial class ServicePage : Page
    {
        private readonly string? _utilsPath = AppPaths.ZapretFolder != null ? Path.Combine(AppPaths.ZapretFolder, "utils") : null;
        private readonly string? _listsPath = AppPaths.ZapretFolder != null ? Path.Combine(AppPaths.ZapretFolder, "lists") : null;
        private readonly string? _binPath = AppPaths.ZapretFolder != null ? Path.Combine(AppPaths.ZapretFolder, "bin") : null;
        private System.Threading.CancellationTokenSource _diagnosticsCts;
        private bool _onLoadChange = false;

        private static readonly SolidColorBrush ColorGreen = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 212, 126));
        private static readonly SolidColorBrush ColorRed = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 112, 112));
        private static readonly SolidColorBrush ColorYellow = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 214, 102));
        private static readonly SolidColorBrush ColorWhite = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240));

        public ServicePage()
        {
            InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += ServicePage_Loaded;
        }

        private void ServicePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            _onLoadChange = true;
            LoadGameFilterSettings();
            UpdateIPSetStatusUI();
            _onLoadChange = false;

            if (ServiceStatus.IsRebooted)
                RebootInfoBadge.Visibility = Visibility.Collapsed;
            else
                RebootInfoBadge.Visibility = Visibility.Visible;
        }

        private void LoadGameFilterSettings()
        {
            string gameFlagFile = Path.Combine(_utilsPath, AppConstants.GameFilterFlagFile);
            if (!File.Exists(gameFlagFile))
            {
                GameFilterComboBox.SelectedIndex = 0; // Отключен
            }
            else
            {
                string mode = File.ReadAllText(gameFlagFile).Trim().ToLower();
                if (mode == "all") GameFilterComboBox.SelectedIndex = 1;
                else if (mode == "tcp") GameFilterComboBox.SelectedIndex = 2;
                else GameFilterComboBox.SelectedIndex = 3;
            }
        }

        private void GameFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Directory.Exists(_utilsPath)) Directory.CreateDirectory(_utilsPath);
            string gameFlagFile = Path.Combine(_utilsPath, AppConstants.GameFilterFlagFile);

            if (!_onLoadChange)
            {
                ServiceStatus.IsRebooted = false;
                RebootInfoBadge.Visibility = Visibility.Visible;
            }
            else
            {
                RebootInfoBadge.Visibility = Visibility.Collapsed;
            }

            switch (GameFilterComboBox.SelectedIndex)
            {
                case 0: // Отключить
                    if (File.Exists(gameFlagFile)) File.Delete(gameFlagFile);
                    break;
                case 1: // All
                    File.WriteAllText(gameFlagFile, "all");
                    break;
                case 2: // TCP
                    File.WriteAllText(gameFlagFile, "tcp");
                    break;
                case 3: // UDP
                    File.WriteAllText(gameFlagFile, "udp");
                    break;
            }
        }

        private void UpdateIPSetStatusUI()
        {
            string listFile = Path.Combine(_listsPath, "ipset-all.txt");
            if (!File.Exists(listFile) || new FileInfo(listFile).Length == 0)
            {
                IPSetStatusText.Text = "Режим: ANY (пустой)";
                return;
            }

            string content = File.ReadAllText(listFile);
            if (content.Contains("203.0.113.113/32"))
            {
                IPSetStatusText.Text = "Режим: NONE (блокировка)";
            }
            else
            {
                IPSetStatusText.Text = "Режим: LOADED (активен)";
            }
        }

        private void IPSetSwitch_Click(object sender, RoutedEventArgs e)
        {
            string listFile = Path.Combine(_listsPath, "ipset-all.txt");
            string backupFile = listFile + ".backup";

            if (IPSetStatusText.Text.Contains("LOADED"))
            {
                if (File.Exists(listFile)) File.Move(listFile, backupFile, true);
                File.WriteAllText(listFile, "203.0.113.113/32");
            }
            else if (IPSetStatusText.Text.Contains("NONE"))
            {
                File.WriteAllText(listFile, ""); // Сбрасываем в Any
            }
            else // Any
            {
                if (File.Exists(backupFile))
                {
                    File.Move(backupFile, listFile, true);
                }
            }
            UpdateIPSetStatusUI();
        }

        private async void UpdateIPSet_Click(object sender, RoutedEventArgs e)
        {
            UpdateIpsetButton.IsEnabled = false;
            IPSetProgress.Visibility = Visibility.Visible;
            IPSetProgress.IsActive = true;

            string listFile = Path.Combine(_listsPath, "ipset-all.txt");

            var popupContent = new StackPanel { Spacing = 4, Padding = new Thickness(4, -10, 4, 2) };
            var titleBlock = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 };
            var subtitleBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"] };

            popupContent.Children.Add(titleBlock);
            popupContent.Children.Add(subtitleBlock);

            try
            {
                using var client = new System.Net.Http.HttpClient();
                var data = await client.GetStringAsync(AppConstants.IPSetUpdateUrl);
                await File.WriteAllTextAsync(listFile, data);

                titleBlock.Text = "Успешно";
                titleBlock.Foreground = ColorGreen;
                subtitleBlock.Text = "Списки IPSet были успешно загружены и обновлены.";
            }
            catch (Exception ex)
            {
                titleBlock.Text = "Ошибка обновления";
                titleBlock.Foreground = ColorRed;
                subtitleBlock.Text = $"Не удалось обновить списки: {ex.Message}";
            }
            finally
            {
                UpdateResultPopup.Content = popupContent;
                IPSetProgress.IsActive = false;
                IPSetProgress.Visibility = Visibility.Collapsed;
                UpdateIpsetButton.IsEnabled = true;
                UpdateIPSetStatusUI();
                UpdateResultPopup.IsOpen = true;

                _ = Task.Delay(4000).ContinueWith(_ =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateResultPopup.IsOpen = false;
                    });
                });
            }
        }

        private async void UpdateHosts_Click(object sender, RoutedEventArgs e)
        {
            string instructionMessage =
                "1. Сейчас откроется вкладка в браузере со свежими правилами.\n" +
                "2. Выделите весь текст (Ctrl + A) и скопируйте его (Ctrl + C).\n" +
                "3. Откройте Блокнот от имени Администратора.\n" +
                "4. Откройте файл по пути: C:\\Windows\\System32\\drivers\\etc\\hosts\n" +
                "5. Вставьте скопированные строки в самый конец файла и сохраните.";

            ContentDialog instructionsDialog = new ContentDialog
            {
                Title = "Инструкция по обновлению hosts",
                Content = instructionMessage,
                PrimaryButtonText = "Открыть хосты и продолжить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await instructionsDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = AppConstants.HostsUpdateUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Не удалось открыть браузер: {ex.Message}");

                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось автоматически открыть браузер. Ссылка: {AppConstants.HostsUpdateUrl}",
                        CloseButtonText = "ОК",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void RunTests_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Запуск конфигурационных тестов",
                Content = "Тесты будут запущены в отдельном окне командной строки с правами администратора.\n\nПожалуйста, внимательно следуйте инструкциям, которые появятся внутри открывшегося окна.",
                PrimaryButtonText = "ОК",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string testScript = Path.Combine(_utilsPath, "test zapret.ps1");
                if (File.Exists(testScript))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{testScript}\"",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Не удалось найти файл скрипта тестирования по пути:\n{testScript}",
                        CloseButtonText = "ОК",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void RunDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsCts = new System.Threading.CancellationTokenSource();
            var token = _diagnosticsCts.Token;

            LogParagraph.Inlines.Clear();
            DiagnosticsLoadingBlock.Visibility = Visibility.Visible;
            DiagnosticsDialog.IsPrimaryButtonEnabled = false;

            var dialogTask = DiagnosticsDialog.ShowAsync();

            try
            {
                PrintLog("=== НАЧАЛО СИСТЕМНОЙ ДИАГНОСТИКИ ===", ColorWhite);
                PrintLog("-------------------------------------\n", ColorWhite);

                await RunDiagnosticChecks(token);

                PrintLog("\n-------------------------------------", ColorWhite);
                PrintLog("=== ДИАГНОСТИКА УСПЕШНО ЗАВЕРШЕНА ===", ColorGreen);
            }
            catch (Exception ex)
            {
                PrintLog($"\n[X] Ошибка выполнения: {ex.Message}", ColorRed);
            }
            finally
            {
                DiagnosticsLoadingBlock.Visibility = Visibility.Collapsed;
                DiagnosticsDialog.IsPrimaryButtonEnabled = true;
            }
        }

        private async Task RunDiagnosticChecks(System.Threading.CancellationToken token)
        {
            // 1. Base Filtering Engine
            PrintLog("Проверка Base Filtering Engine...", ColorWhite);
            bool bfeRunning = await CheckServiceRunningAsync("BFE");
            if (bfeRunning) PrintLog("Base Filtering Engine check passed", ColorGreen);
            else PrintLog("[X] Base Filtering Engine is not running. This service is required for zapret to work", ColorRed);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 2. Proxy Check
            PrintLog("Проверка системного прокси-сервера...", ColorWhite);
            var proxy = GetSystemProxyInfo();
            if (proxy.Enabled)
            {
                PrintLog($"[?] System proxy is enabled: {proxy.Server}", ColorYellow);
                PrintLog("Make sure it's valid or disable it if you don't use a proxy", ColorYellow);
            }
            else PrintLog("Proxy check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 3. TCP Timestamps Check & Auto-Fix
            PrintLog("Проверка TCP timestamps...", ColorWhite);
            bool tcpTimestamps = await CheckTcpTimestampsAsync();
            if (tcpTimestamps) PrintLog("TCP timestamps check passed", ColorGreen);
            else
            {
                PrintLog("[?] TCP timestamps are disabled. Enabling timestamps...", ColorYellow);
                bool fixedTcp = await ExecuteCommandAsync("netsh", "interface tcp set global timestamps=enabled");
                if (fixedTcp) PrintLog("TCP timestamps successfully enabled", ColorGreen);
                else PrintLog("[X] Failed to enable TCP timestamps", ColorRed);
            }
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 4. AdguardSvc.exe process check
            PrintLog("Проверка процесса Adguard...", ColorWhite);
            bool hasAdguard = Process.GetProcessesByName("AdguardSvc").Length > 0;
            if (hasAdguard)
            {
                PrintLog("[X] Adguard process found. Adguard may cause problems with Discord", ColorRed);
                PrintLog("https://github.com/Flowseal/zapret-discord-youtube/issues/417", ColorRed);
            }
            else PrintLog("Adguard check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 5. Killer Services
            PrintLog("Проверка служб Killer Network...", ColorWhite);
            bool hasKiller = await Task.Run(() => CheckServiceExists("Killer"), token);
            if (hasKiller)
            {
                PrintLog("[X] Killer services found. Killer conflicts with zapret", ColorRed);
                PrintLog("https://github.com/Flowseal/zapret-discord-youtube/issues/2512#issuecomment-2821119513", ColorRed);
            }
            else PrintLog("Killer check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 6. Intel Connectivity Network Service
            PrintLog("Проверка Intel Connectivity Network Service...", ColorWhite);
            bool hasIntel = await Task.Run(() => CheckServiceExists("Intel") && CheckServiceExists("Connectivity"), token);
            if (hasIntel)
            {
                PrintLog("[X] Intel Connectivity Network Service found. It conflicts with zapret", ColorRed);
                PrintLog("https://github.com/ValdikSS/GoodbyeDPI/issues/541#issuecomment-2661670982", ColorRed);
            }
            else PrintLog("Intel Connectivity check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 7. Check Point
            PrintLog("Проверка Check Point Antivirus...", ColorWhite);
            bool hasCheckPoint = await Task.Run(() => CheckServiceExists("TracSrvWrapper") || CheckServiceExists("EPWD"), token);
            if (hasCheckPoint)
            {
                PrintLog("[X] Check Point services found. Check Point conflicts with zapret", ColorRed);
                PrintLog("Try to uninstall Check Point", ColorRed);
            }
            else PrintLog("Check Point check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 8. SmartByte
            PrintLog("Проверка SmartByte...", ColorWhite);
            bool hasSmartByte = await Task.Run(() => CheckServiceExists("SmartByte"), token);
            if (hasSmartByte)
            {
                PrintLog("[X] SmartByte services found. SmartByte conflicts with zapret", ColorRed);
                PrintLog("Try to uninstall or disable SmartByte through services.msc", ColorRed);
            }
            else PrintLog("SmartByte check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 9. WinDivert64.sys file check
            PrintLog("Проверка файлов драйвера...", ColorWhite);
            if (Directory.Exists(_binPath) && Directory.GetFiles(_binPath, "*.sys").Length > 0)
            {
                PrintLog("WinDivert file check passed", ColorGreen);
            }
            else
            {
                PrintLog("WinDivert64.sys file NOT found.", ColorRed);
            }
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 10. VPN Services Check
            PrintLog("Поиск активных VPN-служб...", ColorWhite);
            string vpnList = await GetVpnServicesAsync();
            if (!string.IsNullOrEmpty(vpnList))
            {
                PrintLog($"[?] VPN services found: {vpnList}. Some VPNs can conflict with zapret", ColorYellow);
                PrintLog("Make sure that all VPNs are disabled", ColorYellow);
            }
            else PrintLog("VPN check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 11. Secure DNS (DoH) via PowerShell
            PrintLog("Проверка безопасного DNS в Windows...", ColorWhite);
            bool secureDns = await CheckSecureDnsAsync();
            if (!secureDns)
            {
                PrintLog("[?] Make sure you have configured secure DNS in a browser with some non-default DNS service provider,", ColorYellow);
                PrintLog("If you use Windows 11 you can configure encrypted DNS in the Settings to hide this warning", ColorYellow);
            }
            else PrintLog("Secure DNS check passed", ColorGreen);
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 12. Hosts File Check
            PrintLog("Анализ файла hosts...", ColorWhite);
            await CheckHostsFileAsync();
            PrintLog("", ColorWhite);
            if (token.IsCancellationRequested) return;

            // 13. WinDivert Conflict Logic & Auto-Fixes
            PrintLog("Проверка конфликтов WinDivert...", ColorWhite);
            await CheckAndFixWinDivertConflictsAsync(token);
            PrintLog("", ColorWhite);

            // 14. Conflicting Bypass Services
            PrintLog("Поиск сторонних служб обхода...", ColorWhite);
            await CheckConflictingBypassServicesAsync(token);
        }

        private async Task CheckAndFixWinDivertConflictsAsync(System.Threading.CancellationToken token)
        {
            bool winwsRunning = Process.GetProcessesByName("winws").Length > 0;
            bool winDivertActive = await CheckServiceRunningAsync("WinDivert");

            if (!winwsRunning && winDivertActive)
            {
                PrintLog("[?] winws.exe is not running but WinDivert service is active. Attempting to delete WinDivert...", ColorYellow);
                await ExecuteCommandAsync("net", "stop WinDivert");
                await ExecuteCommandAsync("sc", "delete WinDivert");

                bool winDivertStillExists = await CheckServiceExistsInternalAsync("WinDivert");
                if (winDivertStillExists)
                {
                    PrintLog("[X] Failed to delete WinDivert. Checking for conflicting services...", ColorRed);

                    // Удаление GoodbyeDPI если он держит WinDivert
                    if (await CheckServiceExistsInternalAsync("GoodbyeDPI"))
                    {
                        PrintLog("[?] Found conflicting service: GoodbyeDPI. Stopping and removing...", ColorYellow);
                        await ExecuteCommandAsync("net", "stop GoodbyeDPI");
                        bool deleted = await ExecuteCommandAsync("sc", "delete GoodbyeDPI");
                        PrintLog(deleted ? "Successfully removed service: GoodbyeDPI" : "[X] Failed to remove service: GoodbyeDPI", deleted ? ColorGreen : ColorRed);
                    }

                    // Повторная попытка очистки WinDivert
                    PrintLog("[?] Attempting to delete WinDivert again...", ColorYellow);
                    await ExecuteCommandAsync("net", "stop WinDivert");
                    await ExecuteCommandAsync("sc", "delete WinDivert");

                    if (!await CheckServiceExistsInternalAsync("WinDivert"))
                        PrintLog("WinDivert successfully deleted after removing conflicting services", ColorGreen);
                    else
                        PrintLog("[X] WinDivert still cannot be deleted. Check manually if any other bypass is using WinDivert.", ColorRed);
                }
                else PrintLog("WinDivert successfully removed", ColorGreen);
            }
            else PrintLog("WinDivert conflict check passed", ColorGreen);
        }

        private async Task CheckConflictingBypassServicesAsync(System.Threading.CancellationToken token)
        {
            var bypassServices = new List<string> { "GoodbyeDPI", "discordfix_zapret", "winws1", "winws2" };
            var foundBypasses = new List<string>();

            foreach (var service in bypassServices)
            {
                if (token.IsCancellationRequested) return;
                if (await CheckServiceExistsInternalAsync(service)) foundBypasses.Add(service);
            }

            if (foundBypasses.Count > 0)
            {
                PrintLog($"[X] Conflicting bypass services found: {string.Join(" ", foundBypasses)}", ColorRed);
                PrintLog("[!] Для автоматической очистки сторонних служб удалите их через оригинальный service.bat или деинсталлятор конфликтующей утилиты.", ColorYellow);
            }
            else PrintLog("No conflicting bypass services found", ColorGreen);
        }

        private async Task CheckHostsFileAsync()
        {
            string hostsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            if (File.Exists(hostsFile))
            {
                string hostsContent = await File.ReadAllTextAsync(hostsFile);
                if (hostsContent.Contains("youtube.com") || hostsContent.Contains("youtu.be"))
                {
                    PrintLog("[?] Your hosts file contains entries for youtube.com or youtu.be. This may cause problems with YouTube access", ColorYellow);
                }
                else PrintLog("Hosts file check passed", ColorGreen);
            }
        }

        private void PrintLog(string text, SolidColorBrush color)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var run = new Run { Text = text + "\n", Foreground = color };
                LogParagraph.Inlines.Add(run);
            });
        }

        private Task<bool> CheckServiceRunningAsync(string serviceName)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var sc = new System.ServiceProcess.ServiceController(serviceName);
                    sc.Refresh();
                    return sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                }
                catch { return false; }
            });
        }

        private bool CheckServiceExists(string servicePartName)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = "query type= service state= all",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                using var process = Process.Start(startInfo);
                string output = process.StandardOutput.ReadToEnd();
                return output.Contains(servicePartName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private Task<bool> CheckServiceExistsInternalAsync(string serviceName)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var sc = new System.ServiceProcess.ServiceController(serviceName);
                    var name = sc.ServiceName;
                    return true;
                }
                catch { return false; }
            });
        }

        private (bool Enabled, string Server) GetSystemProxyInfo()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                if (key != null)
                {
                    int enabled = (int)(key.GetValue("ProxyEnable") ?? 0);
                    string server = (key.GetValue("ProxyServer") ?? string.Empty).ToString() ?? string.Empty;
                    return (enabled == 1, server);
                }
            }
            catch { }
            return (false, string.Empty);
        }

        private Task<bool> CheckTcpTimestampsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "interface tcp show global",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(startInfo);
                    string output = process.StandardOutput.ReadToEnd();
                    return output.Contains("timestamps", StringComparison.OrdinalIgnoreCase) && output.Contains("enabled", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            });
        }

        private Task<string> GetVpnServicesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = "query type= service state= all",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };
                    using var process = Process.Start(startInfo);
                    string output = process.StandardOutput.ReadToEnd();

                    var foundVpns = new List<string>();
                    using var reader = new StringReader(output);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("SERVICE_NAME", StringComparison.OrdinalIgnoreCase) && line.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':');
                            if (parts.Length > 1) foundVpns.Add(parts[1].Trim());
                        }
                    }
                    return string.Join(", ", foundVpns);
                }
                catch { return ""; }
            });
        }

        private Task<bool> CheckSecureDnsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-ChildItem -Recurse -Path 'HKLM:\\System\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\' | Get-ItemProperty | Where-Object { $_.DohFlags -gt 0 } | Measure-Object | Select-Object -ExpandProperty Count\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(startInfo);
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    if (int.TryParse(output, out int count))
                    {
                        return count > 0;
                    }
                    return false;
                }
                catch { return false; }
            });
        }

        private Task<bool> ExecuteCommandAsync(string fileName, string arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(startInfo);
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
                catch { return false; }
            });
        }
    }
}