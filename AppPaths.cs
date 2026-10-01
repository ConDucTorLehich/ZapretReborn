using System;
using System.IO;
using System.Linq;

namespace ZapretReborn
{
    /// <summary>
    /// Хелпер-класс для определения путей приложения
    /// </summary>
    public static class AppPaths
    {
        private static string? _zapretFolder;

        public static string ApplicationDirectory =>
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        /// <summary>
        /// Возвращает путь к папке с Zapret скриптами
        /// </summary>
        public static string? ZapretFolder
        {
            get
            {
                // Кэшируем результат, если папка существует
                if (_zapretFolder != null && Directory.Exists(_zapretFolder))
                    return _zapretFolder;

                foreach (string searchDirectory in GetZapretSearchDirectories())
                {
                    foreach (var pattern in new[] { "zapret-*", "zapret-discord-*" })
                    {
                        var matchingDirs = Directory.GetDirectories(searchDirectory, pattern);
                        if (matchingDirs.Length > 0)
                        {
                            _zapretFolder = matchingDirs[0];
                            return _zapretFolder;
                        }
                    }
                }

                return null;
            }
        }

        private static string[] GetZapretSearchDirectories()
        {
            return new[]
            {
                ApplicationDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ZapretReborn")
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToArray();
        }

        /// <summary>
        /// Сбрасывает кэш папки Zapret (нужно после загрузки новых скриптов)
        /// </summary>
        public static void ResetCache()
        {
            _zapretFolder = null;
        }

        /// <summary>
        /// Получает текущую версию скриптов из service.bat, любого .bat файла или имени папки
        /// </summary>
        public static string GetCurrentScriptVersion()
        {
            try
            {
                string? zapretFolder = null;
                
                foreach (string searchDirectory in GetZapretSearchDirectories())
                {
                    foreach (var pattern in new[] { "zapret-*", "zapret-discord-*" })
                    {
                        var matchingDirectories = Directory.GetDirectories(searchDirectory, pattern);
                        if (matchingDirectories.Length > 0)
                        {
                            zapretFolder = matchingDirectories[0];
                            break;
                        }
                    }

                    if (zapretFolder != null)
                    {
                        break;
                    }
                }
                
                if (zapretFolder == null)
                {
                    return AppConstants.CurrentScriptVersion;
                }

                // Пытаемся прочитать из service.bat (для совместимости)
                string serviceFilePath = Path.Combine(zapretFolder, "service.bat");
                if (File.Exists(serviceFilePath))
                {
                    foreach (var line in File.ReadAllLines(serviceFilePath))
                    {
                        var trimmedLine = line.Trim();
                        // Поддерживаем разные форматы: set "LOCAL_VERSION=..., set LOCAL_VERSION=..., LOCAL_VERSION=...
                        if (trimmedLine.StartsWith("set \"LOCAL_VERSION=") ||
                            trimmedLine.StartsWith("set LOCAL_VERSION=") ||
                            trimmedLine.StartsWith("LOCAL_VERSION="))
                        {
                            var parts = trimmedLine.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                return parts[1].Trim().Trim('"', '\'', ' ');
                            }
                        }
                    }
                }

                // Пробуем найти версию в любом .bat файле
                var batFiles = Directory.GetFiles(zapretFolder, "*.bat", SearchOption.AllDirectories);
                foreach (var batFile in batFiles)
                {
                    foreach (var line in File.ReadAllLines(batFile))
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("set \"LOCAL_VERSION=") ||
                            trimmedLine.StartsWith("set LOCAL_VERSION=") ||
                            trimmedLine.StartsWith("LOCAL_VERSION="))
                        {
                            var parts = trimmedLine.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                return parts[1].Trim().Trim('"', '\'', ' ');
                            }
                        }
                    }
                }

                // Резерв: парсим имя папки (например, zapret-discord-youtube-1.10.3)
                var folderName = Path.GetFileName(zapretFolder) ?? string.Empty;
                var match = System.Text.RegularExpressions.Regex.Match(folderName, @"\d+\.\d+\.\d+[a-z]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value;
                }

                return AppConstants.CurrentScriptVersion;
            }
            catch
            {
                return AppConstants.CurrentScriptVersion;
            }
        }
    }
}