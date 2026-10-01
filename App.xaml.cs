using Microsoft.UI.Xaml;

namespace ZapretReborn
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }
        public static bool IsUpdateAvailable { get; set; } = false;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            
            // Применяем сохраненную тему при старте приложения
            ApplySavedTheme(MainWindow);
            
            MainWindow.Activate();
        }
        
        private void ApplySavedTheme(Window window)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ZapretReborn", writable: false);
                if (key != null)
                {
                    var value = key.GetValue("Theme");
                    if (value != null && int.TryParse(value.ToString(), out int index))
                    {
                        ElementTheme selectedTheme = index switch
                        {
                            0 => ElementTheme.Default,
                            1 => ElementTheme.Light,
                            2 => ElementTheme.Dark,
                            _ => ElementTheme.Default
                        };
                        
                        if (window.Content is FrameworkElement rootElement)
                        {
                            rootElement.RequestedTheme = selectedTheme;
                        }
                    }
                }
            }
            catch { }
        }
    }
}