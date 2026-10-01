using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
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

        public MainWindow()
        {
            InitializeComponent();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += RootElement_Loaded;
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

            nvZapret.SelectedItem = Home;

            CustomizeTitleBar();
            SetAppIcon();

            // 1. Запускаем проверку сразу при старте приложения
            _ = CheckAllUpdatesAsync();

            // 2. Настраиваем таймер на повторение
            StartUpdateTimer();
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
                    _isUiUpdateAvailable = uiUpdateResult.HasUpdate;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки UI обновлений: {ex.Message}");
                }

                // 2. Проверяем обновления скриптов через GitHub API
                try
                {
                    var scriptUpdateResult = await UpdateService.CheckScriptUpdatesAsync();
                    _isScriptUpdateAvailable = scriptUpdateResult.HasUpdate;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений скриптов: {ex.Message}");
                }

                // Обновляем UI
                if (_isUiUpdateAvailable || _isScriptUpdateAvailable)
                {
                    UpdateBadge.Visibility = Visibility.Visible;
                    App.IsUpdateAvailable = true;

                    string tooltipText;
                    if (_isUiUpdateAvailable && _isScriptUpdateAvailable)
                        tooltipText = " Доступны обновления приложения и скриптов!";
                    else if (_isUiUpdateAvailable)
                        tooltipText = " Доступно обновление приложения!";
                    else
                        tooltipText = " Доступно обновление скриптов zapret!";

                    ToolTipService.SetToolTip(Settings, tooltipText);
                }
                else
                {
                    UpdateBadge.Visibility = Visibility.Collapsed;
                    App.IsUpdateAvailable = false;
                }
            }
            finally
            {
                _isCheckingUpdates = false;
            }
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
                    var buttonColor = isDark ? Colors.White : Colors.Black;
                    var hoverColor = isDark ? Windows.UI.Color.FromArgb(30, 255, 255, 255) : Windows.UI.Color.FromArgb(30, 0, 0, 0);

                    titleBar.ButtonForegroundColor = buttonColor;
                    titleBar.ButtonHoverBackgroundColor = hoverColor;
                    titleBar.ButtonHoverForegroundColor = buttonColor;
                    titleBar.ButtonPressedBackgroundColor = isDark ? Windows.UI.Color.FromArgb(80, 255, 255, 255) : Windows.UI.Color.FromArgb(80, 0, 0, 0);
                    titleBar.ButtonInactiveForegroundColor = isDark ? Colors.Gray : Colors.DarkGray;
                    
                    // Убедимся, что кнопки видны на светлом фоне - используем темно-серый для активного состояния
                    // и черный для наведения, чтобы было видно на любом фоне
                    if (!isDark)
                    {
                        titleBar.ButtonForegroundColor = Colors.Black;
                        titleBar.ButtonHoverForegroundColor = Colors.Black;
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(150, 0, 0, 0);
                    }
                }
            }
        }

        private void SetAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
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
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
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
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
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

            bool success = await ZapretDownloader.DownloadAndExtractLatestAsync((status, percent) =>
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
                Title = success ? "Загрузка завершена" : "Ошибка загрузки",
                Content = success
                    ? "Все компоненты успешно установлены! Теперь вы можете выбрать и запустить нужный скрипт."
                    : "Не удалось автоматическая загрузка. Открыть страницу релизов в браузере?",
                PrimaryButtonText = success ? "Отлично" : "Открыть сайт",
                CloseButtonText = success ? null : "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var res = await resultDialog.ShowAsync();

            if (success)
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
