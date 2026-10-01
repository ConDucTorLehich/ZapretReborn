using System;
using System.IO;

namespace ZapretReborn
{
    /// <summary>
    /// Хелпер-класс для определения путей приложения
    /// </summary>
    public static class AppPaths
    {
        private static string? _zapretFolder;

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

                string baseDir = AppContext.BaseDirectory;

                // Ищем любую папку, начинающуюся на zapret- в директории приложения
                var matchingDirs = Directory.GetDirectories(baseDir, "zapret-*");
                if (matchingDirs.Length > 0)
                {
                    _zapretFolder = matchingDirs[0];
                    return _zapretFolder;
                }

                // Papka ne naidena
                return null;
            }
        }

        /// <summary>
        /// Сбрасывает кэш папки Zapret (нужно после загрузки новых скриптов)
        /// </summary>
        public static void ResetCache()
        {
            _zapretFolder = null;
        }

        /// <summary>
        /// Получает текущую версию скриптов из service.bat или имени папки
        /// </summary>
        public static string GetCurrentScriptVersion()
        {
            try
            {
                string appDirectory = AppContext.BaseDirectory;
                var matchingDirectories = Directory.GetDirectories(appDirectory, "zapret-*");
                if (matchingDirectories.Length == 0)
                {
                    return AppConstants.CurrentScriptVersion;
                }

                string zapretFolder = matchingDirectories[0];

                // Пытаемся прочитать из service.bat
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

                // Резерв: парсим имя папки
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