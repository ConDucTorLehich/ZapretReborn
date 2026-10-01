using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ZapretReborn
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer _updateTimer;
        private bool _isCheckingUpdates = false;
        private bool _isUiUpdateAvailable = false;
        private bool _isScriptUpdateAvailable = false;
        private string? _installedUiVersionThisSession;
        private string? _installedScriptVersionThisSession;
        private bool _isStoppingScriptsForClose;
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += RootElement_Loaded;
                rootElement.ActualThemeChanged += RootElement_ActualThemeChanged;
            }
            AppWindow.Title = "ZapretReborn";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(340, 575));
            AppWindow.Move(new Windows.Graphics.PointInt32(1580, 505));

            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = false;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(true, true);
            AppWindow.SetPresenter(presenter);
            AppWindow.Closing += AppWindow_Closing;

            nvZapret.SelectedItem = Home;

            CustomizeTitleBar();
            SetAppIcon();

            // 1. Запускаем проверку сразу при старте приложения
            _ = CheckAllUpdatesAsync();

            // 2. Настраиваем таймер на повторение
            StartUpdateTimer();
        }

        private async void AppWindow_Closing(
            AppWindow sender,
            Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (_allowClose || Process.GetProcessesByName("winws").Length == 0)
            {
                return;
            }

            args.Cancel = true;
            if (_isStoppingScriptsForClose)
            {
                return;
            }

            _isStoppingScriptsForClose = true;
            try
            {
                bool stopped = await HomePage.StopRunningScriptsAsync();
                if (stopped)
                {
                    _allowClose = true;
                    _updateTimer.Stop();
                    Close();
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "Не удалось остановить Zapret",
                    Content = "Процесс winws всё ещё работает. Подтвердите запрос контроля учетных записей Windows, чтобы остановить его, затем нажмите «Повторить».",
                    PrimaryButtonText = "Повторить",
                    CloseButtonText = "Остаться в приложении",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = ((FrameworkElement)Content).XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка остановки скрипта при закрытии: {ex.Message}");
            }
            finally
            {
                _isStoppingScriptsForClose = false;
            }
        }

        private async void RootElement_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckAndPromptForZapretAsync();
        }

        private void StartUpdateTimer()
        {
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = AppConstants.UpdateCheckInterval;
            _updateTimer.Tick += async (s, e) =>
            {
                if (!_isCheckingUpdates)
                {
                    await CheckAllUpdatesAsync();
                }
            };
            _updateTimer.Start();
        }

        private void RootElement_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateTitleBarButtonsColor();
        }

        private async Task CheckAllUpdatesAsync()
        {
            if (_isCheckingUpdates) return;

            _isCheckingUpdates = true;

            try
            {
                _isUiUpdateAvailable = false;
                _isScriptUpdateAvailable = false;

                // 1. Проверяем обновления UI приложения через GitHub API
                try
                {
                    var uiUpdateResult = await UpdateService.CheckUiUpdatesAsync();
                    _isUiUpdateAvailable = uiUpdateResult.HasUpdate &&
                        !string.Equals(uiUpdateResult.LatestVersion, _installedUiVersionThisSession, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки UI обновлений: {ex.Message}");
                }

                // 2. Проверяем обновления скриптов через GitHub API
                try
                {
                    var scriptUpdateResult = await UpdateService.CheckScriptUpdatesAsync();
                    _isScriptUpdateAvailable = scriptUpdateResult.HasUpdate &&
                        !string.Equals(scriptUpdateResult.LatestVersion, _installedScriptVersionThisSession, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений скриптов: {ex.Message}");
                }

                UpdateUpdateBadge();
            }
            finally
            {
                _isCheckingUpdates = false;
            }
        }

        public void SetUpdateAvailability(
            bool uiUpdateAvailable,
            bool scriptUpdateAvailable,
            string? installedUiVersion,
            string? installedScriptVersion)
        {
            if (!string.IsNullOrWhiteSpace(installedUiVersion))
            {
                _installedUiVersionThisSession = installedUiVersion;
            }

            if (!string.IsNullOrWhiteSpace(installedScriptVersion))
            {
                _installedScriptVersionThisSession = installedScriptVersion;
            }

            _isUiUpdateAvailable = uiUpdateAvailable;
            _isScriptUpdateAvailable = scriptUpdateAvailable;
            UpdateUpdateBadge();
        }

        private void UpdateUpdateBadge()
        {
            bool hasAvailableUpdates = _isUiUpdateAvailable || _isScriptUpdateAvailable;
            UpdateBadge.Visibility = hasAvailableUpdates ? Visibility.Visible : Visibility.Collapsed;
            App.IsUpdateAvailable = hasAvailableUpdates;

            if (!hasAvailableUpdates)
            {
                ToolTipService.SetToolTip(Settings, "Настройки");
                return;
            }

            string tooltipText = _isUiUpdateAvailable && _isScriptUpdateAvailable
                ? "Доступны обновления приложения и скриптов!"
                : _isUiUpdateAvailable
                    ? "Доступно обновление приложения!"
                    : "Доступно обновление скриптов zapret!";
            ToolTipService.SetToolTip(Settings, tooltipText);
        }

        private void CustomizeTitleBar()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                UpdateTitleBarButtonsColor();
            }
        }

        private void UpdateTitleBarButtonsColor()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = AppWindow.TitleBar;
                var rootElement = this.Content as FrameworkElement;
                if (rootElement != null)
                {
                    bool isDark = rootElement.ActualTheme == ElementTheme.Dark;
                    
                    // Для светлой темы используем темные кнопки, для темной - светлые
                    // Это гарантирует, что кнопки всегда видны
                    titleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
                    titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
                    titleBar.ButtonInactiveForegroundColor = isDark ? 
                        Windows.UI.Color.FromArgb(150, 255, 255, 255) : 
                        Windows.UI.Color.FromArgb(150, 0, 0, 0);
                    
                    // Фон кнопок при наведении и нажатии
                    titleBar.ButtonHoverBackgroundColor = isDark ? 
                        Windows.UI.Color.FromArgb(30, 255, 255, 255) : 
                        Windows.UI.Color.FromArgb(30, 0, 0, 0);
                    titleBar.ButtonPressedBackgroundColor = isDark ? 
                        Windows.UI.Color.FromArgb(80, 255, 255, 255) : 
                        Windows.UI.Color.FromArgb(80, 0, 0, 0);
                }
            }
        }

        private void SetAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppPaths.ApplicationDirectory, "appicon.ico");
                if (File.Exists(iconPath))
                {
                    this.AppWindow.SetIcon(iconPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось установить иконку окна: {ex.Message}");
            }
        }

        private void nvZapret_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? string.Empty;

                Type pageType = tag switch
                {
                    "Home" => typeof(HomePage),
                    "Service" => typeof(ServicePage),
                    "Settings" => typeof(SettingsPage),
                    _ => typeof(HomePage)
                };

                if (contentFrame.CurrentSourcePageType != pageType)
                {
                    contentFrame.Navigate(pageType);
                }
            }
        }

        private async Task CheckAndPromptForZapretAsync()
        {
            if (AppPaths.ZapretFolder == null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Компоненты Zapret не найдены",
                    Content = "В папке с приложением не найдена директория со скриптами Zapret.\n\n" +
                              "Вы хотите скачать и распаковать их автоматически или открыть страницу загрузки, чтобы сделать это вручную?",
                    PrimaryButtonText = "Автомат",
                    SecondaryButtonText = "Вручную",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await ShowDownloadProgressDialogAsync();
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    await ZapretDownloader.OpenReleasesInBrowserAsync();
                }
            }
        }

        private async Task ShowDownloadProgressDialogAsync()
        {
            var statusText = new TextBlock
            {
                Text = "Инициализация...",
                FontSize = 13,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
                Margin = new Thickness(0, 0, 0, 8)
            };

            var progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Value = 0,
                Maximum = 100,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 4
            };

            var percentText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
                Margin = new Thickness(0, 4, 0, 0)
            };

            var contentContainer = new StackPanel
            {
                Spacing = 4,
                Width = 380,
                Margin = new Thickness(0, 8, 0, 0)
            };

            contentContainer.Children.Add(statusText);
            contentContainer.Children.Add(progressBar);
            contentContainer.Children.Add(percentText);

            var progressDialog = new ContentDialog
            {
                Title = "Загрузка компонентов Zapret",
                Content = contentContainer,
                XamlRoot = this.Content.XamlRoot
            };

            _ = progressDialog.ShowAsync();

            var result = await ZapretDownloader.DownloadAndExtractLatestAsync((status, percent) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusText.Text = status;

                    if (percent.HasValue)
                    {
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = percent.Value;
                        percentText.Text = $"{Math.Round(percent.Value)}%";
                    }
                    else
                    {
                        progressBar.IsIndeterminate = true;
                        percentText.Text = "";
                    }
                });
            });

            progressDialog.Hide();

            var resultDialog = new ContentDialog
            {
                Title = result.Success ? "Загрузка завершена" : "Ошибка загрузки",
                Content = result.Success
                    ? "Все компоненты успешно установлены! Теперь вы можете выбрать и запустить нужный скрипт."
                    : $"Не удалось загрузить скрипты.\n\nПричина:\n{result.ErrorMessage}\n\nОтправьте скриншот этого сообщения разработчикам, если ошибка повторяется.",
                PrimaryButtonText = result.Success ? "Отлично" : "Открыть сайт",
                CloseButtonText = result.Success ? null : "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var res = await resultDialog.ShowAsync();

            if (result.Success)
            {
                if (contentFrame.Content is HomePage homePage)
                {
                    homePage.RefreshScripts();
                }
            }
            else if (res == ContentDialogResult.Primary)
            {
                await ZapretDownloader.OpenReleasesInBrowserAsync();
            }
        }
    }
}
