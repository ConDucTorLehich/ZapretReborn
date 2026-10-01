using System;

namespace ZapretReborn
{
    /// <summary>
    /// Статический класс для хранения констант приложения
    /// </summary>
    public static class AppConstants
    {
        // Версии приложения
        public const string CurrentUiVersion = "2.0.0";
        public const string CurrentScriptVersion = "2.0.0";

        // URL для проверки обновлений скриптов (Flowseal)
        public const string ScriptVersionUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/version.txt";
        
        // URL для загрузки IPSet (Flowseal)
        public const string IPSetUpdateUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        
        // URL для хостов (Flowseal)
        public const string HostsUpdateUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts";

        // GitHub API для обновлений UI приложения (ConDucTorLehich)
        public const string GitHubUiOwner = "ConDucTorLehich";
        public const string GitHubUiRepo = "ZapretReborn";
        public const string GitHubUiApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";
        
        // GitHub API для обновлений скриптов (Flowseal)
        public const string GitHubScriptOwner = "Flowseal";
        public const string GitHubScriptRepo = "zapret-discord-youtube";
        public const string GitHubScriptApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";

        // Названия файлов конфигурации
        public const string ConfigFileName = "config.ini";
        public const string GameFilterFlagFile = "game_filter.enabled";
        public const string PatchMarkerFile = ".patched";
        
        // Имя приложения для автозагрузки
        public const string AppName = "ZapretReborn";
        
        // Таймауты
        public static TimeSpan DefaultHttpTimeout => TimeSpan.FromSeconds(5);
        public static TimeSpan UpdateCheckInterval => TimeSpan.FromMinutes(5);
        
        // Интервалы ожидания
        public static TimeSpan ScriptStartupWait => TimeSpan.FromSeconds(2);
        public static TimeSpan ScriptCheckTimeout => TimeSpan.FromSeconds(10);
        public static TimeSpan ScriptCheckInterval => TimeSpan.FromSeconds(1);
        public static TimeSpan TrafficInterceptDelay => TimeSpan.FromMilliseconds(2500);
        public static TimeSpan CleanupDelay => TimeSpan.FromSeconds(1);
    }
}
