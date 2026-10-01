using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace ZapretReborn
{
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class ZapretDownloader
    {
        private const string GitHubReleasesUrl = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";
        public const string ReleasesWebUrl = "https://github.com/Flowseal/zapret-discord-youtube/releases";

        public static async Task OpenReleasesInBrowserAsync()
        {
            await Launcher.LaunchUriAsync(new Uri(ReleasesWebUrl));
        }
        
        /// <summary>
        /// Проверяет, используется ли директория каким-либо процессом
        /// </summary>
        private static bool IsDirectoryInUse(string directoryPath)
        {
            try
            {
                // Пробуем получить доступ к директории
                Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<DownloadResult> DownloadAndExtractLatestAsync(Action<string, double?>? progressCallback = null)
        {
            string? tempZipPath = null;
            try
            {
                progressCallback?.Invoke("Получение информации о релизе...", null);

                var handler = new HttpClientHandler 
                {
                    AllowAutoRedirect = true,
                    // Автоматическое разрешение перенаправлений
                    MaxAutomaticRedirections = 5,
                    // Обходим проблемы с SSL сертификатами в self-contained приложениях
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => 
                    {
                        // Доверяем всем сертификатам для GitHub
                        return true;
                    }
                };
                
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZapretReborn/2.0");
                client.Timeout = TimeSpan.FromSeconds(30); // Таймаут 30 секунд

                string jsonResponse;
                try
                {
                    jsonResponse = await client.GetStringAsync(GitHubReleasesUrl);
                }
                catch (HttpRequestException ex)
                {
                    string errorMsg = ex.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Forbidden => "GitHub API заблокирован или превышен лимит запросов",
                        System.Net.HttpStatusCode.NotFound => "Репозиторий Flowseal не найден",
                        System.Net.HttpStatusCode.Unauthorized => "Требуется авторизация в GitHub API",
                        _ => $"Ошибка сети: {ex.Message}"
                    };
                    return new DownloadResult { Success = false, ErrorMessage = errorMsg };
                }
                catch (TaskCanceledException)
                {
                    return new DownloadResult { Success = false, ErrorMessage = "Таймаут: превышено время ожидания ответа сервера" };
                }
                catch (Exception ex)
                {
                    return new DownloadResult { Success = false, ErrorMessage = $"Ошибка при получении информации о релизе: {ex.Message}" };
                }

                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                string downloadUrl = string.Empty;

                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            progressCallback?.Invoke($"Найден архив: {name}", null);
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    const string errorMsg = "В последнем релизе Flowseal не найден ZIP-архив. " +
                        "Скачайте архив вручную с: https://github.com/Flowseal/zapret-discord-youtube/releases";
                    return new DownloadResult { Success = false, ErrorMessage = errorMsg };
                }

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                
                // Проверяем статус ответа
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound => "Файл архива не найден. Возможно, он был удален или переименован.",
                        System.Net.HttpStatusCode.Forbidden => "Доступ запрещен. Возможно, GitHub блокирует запросы.",
                        System.Net.HttpStatusCode.TooManyRequests => "Слишком много запросов. Подождите несколько минут и попробуйте позже.",
                        _ => $"Ошибка HTTP: {response.StatusCode} - {response.ReasonPhrase}"
                    };
                    return new DownloadResult { Success = false, ErrorMessage = errorMessage };
                }

                long? totalBytes = response.Content.Headers.ContentLength;
                tempZipPath = Path.Combine(Path.GetTempPath(), $"zapret_{Guid.NewGuid():N}.zip");

                using (var downloadStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalReadBytes = 0;
                    int bytesRead;

                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalReadBytes += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            double progressPercentage = (double)totalReadBytes / totalBytes.Value * 100;
                            double mbRead = totalReadBytes / 1024.0 / 1024.0;
                            double mbTotal = totalBytes.Value / 1024.0 / 1024.0;
                            progressCallback?.Invoke($"Загрузка: {mbRead:F1} МБ из {mbTotal:F1} МБ", progressPercentage);
                        }
                        else
                        {
                            progressCallback?.Invoke($"Загрузка: {totalReadBytes / 1024 / 1024} МБ", null);
                        }
                    }
                }

                try
                {
                    using var archive = ZipFile.OpenRead(tempZipPath);
                    if (archive.Entries.Count == 0)
                    {
                        File.Delete(tempZipPath);
                        return new DownloadResult
                        {
                            Success = false,
                            ErrorMessage = "Загруженный ZIP-архив пуст. Попробуйте повторить загрузку."
                        };
                    }
                }
                catch (InvalidDataException ex)
                {
                    File.Delete(tempZipPath);
                    return new DownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"Загруженный файл не является целым ZIP-архивом: {ex.Message}"
                    };
                }

                progressCallback?.Invoke("Проверка запущенных процессов...", null);
                
                // Проверяем, не запущены ли скрипты или winws процессы
                bool scriptsRunning = false;
                try
                {
                    var runningProcesses = System.Diagnostics.Process.GetProcesses();
                    scriptsRunning = runningProcesses.Any(p => 
                        !string.IsNullOrEmpty(p.ProcessName) && 
                        (p.ProcessName.Equals("winws", StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    progressCallback?.Invoke($"Ошибка проверки процессов: {ex.Message}", null);
                    // В случае ошибки проверки, все равно продолжаем, но предупреждаем
                    scriptsRunning = false;
                }
                
                if (scriptsRunning)
                {
                    string errorMsg = "Обнаружены запущенные скрипты Zapret. Закройте их перед обновлением и попробуйте снова.";
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                    return new DownloadResult { Success = false, ErrorMessage = errorMsg };
                }
                
                progressCallback?.Invoke("Проверка прав на запись...", null);
                string targetDirectory = AppPaths.ApplicationDirectory;
                
                // Проверяем, можно ли писать в текущую папку
                bool canWriteToAppDir = false;
                try
                {
                    string testFile = Path.Combine(targetDirectory, ".write_test.txt");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    canWriteToAppDir = true;
                }
                catch
                {
                    canWriteToAppDir = false;
                }
                
                if (!canWriteToAppDir)
                {
                    progressCallback?.Invoke("Нет прав на запись в папку приложения. Попробуем использовать рабочий стол...", null);
                    // Пробуем сохранить в папку на рабочем столе
                    targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ZapretReborn");
                    try
                    {
                        if (!Directory.Exists(targetDirectory))
                        {
                            Directory.CreateDirectory(targetDirectory);
                        }
                        canWriteToAppDir = true;
                        progressCallback?.Invoke($"Будем устанавливать в: {targetDirectory}", null);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"Нет прав на запись ни в папку приложения, ни на рабочий стол: {ex.Message}";
                        if (File.Exists(tempZipPath))
                        {
                            File.Delete(tempZipPath);
                        }
                        return new DownloadResult { Success = false, ErrorMessage = errorMsg };
                    }
                }
                
                progressCallback?.Invoke("Удаление старой версии...", null);
                
                // Удаляем старые папки zapret-* только если можно писать в текущую папку
                if (canWriteToAppDir && targetDirectory == AppPaths.ApplicationDirectory)
                {
                    var oldZapretFolders = Directory.GetDirectories(targetDirectory, "zapret-*");
                    foreach (var oldFolder in oldZapretFolders)
                    {
                        try
                        {
                            // Проверяем, что папка не используется
                            if (!IsDirectoryInUse(oldFolder))
                            {
                                Directory.Delete(oldFolder, true);
                                progressCallback?.Invoke($"Удалена папка: {Path.GetFileName(oldFolder)}", null);
                            }
                            else
                            {
                                progressCallback?.Invoke($"Папка {Path.GetFileName(oldFolder)} используется. Пропускаем.", null);
                            }
                        }
                        catch (Exception ex)
                        {
                            progressCallback?.Invoke($"Ошибка удаления папки {Path.GetFileName(oldFolder)}: {ex.Message}", null);
                            // Продолжаем с другими папками
                            continue;
                        }
                    }
                }
                
                progressCallback?.Invoke("Распаковка архива...", null);
                
                ZipFile.ExtractToDirectory(tempZipPath, targetDirectory, overwriteFiles: true);

                progressCallback?.Invoke("Настройка general-скриптов...", null);
                int patchedGeneralFiles = ConfigureGeneralBatchFiles(targetDirectory);

                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
                TryDeleteLegacyTempArchive();

                progressCallback?.Invoke($"Настроено файлов general*: {patchedGeneralFiles}", null);
                progressCallback?.Invoke("Завершено!", 100);
                return new DownloadResult { Success = true, ErrorMessage = null };
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Ошибка: {ex.Message}", null);
                return new DownloadResult { Success = false, ErrorMessage = $"Неожиданная ошибка: {ex.Message}" };
            }
            finally
            {
                if (tempZipPath != null && File.Exists(tempZipPath))
                {
                    try
                    {
                        File.Delete(tempZipPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Не удалось удалить временный ZIP {tempZipPath}: {ex.Message}");
                    }
                }
            }
        }

        private static void TryDeleteLegacyTempArchive()
        {
            string legacyArchivePath = Path.Combine(Path.GetTempPath(), "zapret_latest.zip");
            try
            {
                if (File.Exists(legacyArchivePath))
                {
                    File.Delete(legacyArchivePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось удалить устаревший ZIP {legacyArchivePath}: {ex.Message}");
            }
        }

        private static int ConfigureGeneralBatchFiles(string targetDirectory)
        {
            var zapretDirectories = Directory.GetDirectories(targetDirectory, "zapret-*", SearchOption.TopDirectoryOnly);
            var generalFiles = zapretDirectories
                .SelectMany(directory => Directory.GetFiles(directory, "general*.bat", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (generalFiles.Length == 0)
            {
                throw new InvalidDataException("В загруженном архиве не найдены файлы general*.bat.");
            }

            foreach (string filePath in generalFiles)
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                Encoding encoding;
                int preambleLength;

                if (fileBytes.Length >= 3 &&
                    fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
                {
                    encoding = new UTF8Encoding(false, true);
                    preambleLength = 3;
                }
                else if (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
                {
                    encoding = new UnicodeEncoding(false, false, true);
                    preambleLength = 2;
                }
                else if (fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF)
                {
                    encoding = new UnicodeEncoding(true, false, true);
                    preambleLength = 2;
                }
                else
                {
                    encoding = new UTF8Encoding(false, true);
                    preambleLength = 0;
                }

                string content = encoding.GetString(fileBytes, preambleLength, fileBytes.Length - preambleLength);
                string newline = content.Contains("\r\n", StringComparison.Ordinal)
                    ? "\r\n"
                    : content.Contains('\n') ? "\n" : "\r";
                string[] lines = content.Split(new[] { newline }, StringSplitOptions.None);

                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string updatedLine = lines[i].Replace("/min", "/B", StringComparison.OrdinalIgnoreCase);
                    if (!string.Equals(updatedLine, lines[i], StringComparison.Ordinal))
                    {
                        lines[i] = updatedLine;
                        changed = true;
                    }

                    string leadingWhitespace = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
                    string command = lines[i].TrimStart();
                    bool isTargetCall =
                        string.Equals(command, "call service.bat status_zapret", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(command, "call service.bat check_updates", StringComparison.OrdinalIgnoreCase);

                    if (isTargetCall)
                    {
                        lines[i] = leadingWhitespace + "::" + command;
                        changed = true;
                    }
                }

                if (changed)
                {
                    string updatedContent = string.Join(newline, lines);
                    byte[] updatedBytes = encoding.GetBytes(updatedContent);
                    if (preambleLength > 0)
                    {
                        byte[] bytesWithPreamble = new byte[preambleLength + updatedBytes.Length];
                        Buffer.BlockCopy(fileBytes, 0, bytesWithPreamble, 0, preambleLength);
                        Buffer.BlockCopy(updatedBytes, 0, bytesWithPreamble, preambleLength, updatedBytes.Length);
                        updatedBytes = bytesWithPreamble;
                    }

                    File.WriteAllBytes(filePath, updatedBytes);
                }
            }

            return generalFiles.Length;
        }
    }
}