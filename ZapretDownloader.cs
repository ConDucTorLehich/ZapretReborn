using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace ZapretReborn
{
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

        public static async Task<bool> DownloadAndExtractLatestAsync(Action<string, double?>? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke("Получение информации о релизе...", null);

                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZapretReborn/1.0");

                var jsonResponse = await client.GetStringAsync(GitHubReleasesUrl);
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
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new Exception("Не найден .zip архив в релизе.");
                }

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                string tempZipPath = Path.Combine(Path.GetTempPath(), "zapret_latest.zip");

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

                progressCallback?.Invoke("Проверка запущенных процессов...", null);
                
                // Проверяем, не запущены ли скрипты или winws процессы
                bool scriptsRunning = false;
                try
                {
                    var runningProcesses = System.Diagnostics.Process.GetProcesses();
                    scriptsRunning = runningProcesses.Any(p => 
                        !string.IsNullOrEmpty(p.ProcessName) && 
                        (p.ProcessName.Equals("winws", StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Equals("python", StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Equals("node", StringComparison.OrdinalIgnoreCase) ||
                         p.ProcessName.Contains("zapret", StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    progressCallback?.Invoke($"Ошибка проверки процессов: {ex.Message}", null);
                    // В случае ошибки проверки, все равно продолжаем, но предупреждаем
                    scriptsRunning = false;
                }
                
                if (scriptsRunning)
                {
                    progressCallback?.Invoke("Ошибка: Обнаружены запущенные скрипты или winws процессы. Остановите их перед обновлением.", null);
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                    return false;
                }
                
                progressCallback?.Invoke("Удаление старой версии...", null);
                string targetDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Удаляем старые папки zapret-*
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
                
                progressCallback?.Invoke("Распаковка архива...", null);
                ZipFile.ExtractToDirectory(tempZipPath, targetDirectory, overwriteFiles: true);

                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }

                progressCallback?.Invoke("Завершено!", 100);
                return true;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Ошибка: {ex.Message}", null);
                return false;
            }
        }
    }
}