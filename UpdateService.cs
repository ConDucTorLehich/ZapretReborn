using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZapretReborn;

/// <summary>
/// Результат проверки обновлений
/// </summary>
public class UpdateResult
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Возвращает текущую версию UI приложения из константы
    /// </summary>
    public static string GetCurrentUiVersion()
    {
        return AppConstants.CurrentUiVersion;
    }
}

/// <summary>
/// Результат проверки обновлений скриптов
/// </summary>
public class ScriptUpdateResult
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// Сервис для проверки и установки обновлений
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient _httpClient;

    static UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZapretRebornApp");
    }

    // UI приложения (ConDucTorLehich)
    private static string UiApiUrl => string.Format(AppConstants.GitHubUiApiUrl, AppConstants.GitHubUiOwner, AppConstants.GitHubUiRepo);

    // Скриптов (Flowseal)
    private static string ScriptApiUrl => string.Format(AppConstants.GitHubScriptApiUrl, AppConstants.GitHubScriptOwner, AppConstants.GitHubScriptRepo);

    /// <summary>
    /// Проверяет обновления UI приложения через GitHub API
    /// </summary>
    /// <param name="progress">Опциональный прогресс для отображения</param>
    public static async Task<UpdateResult> CheckUiUpdatesAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Проверка обновлений UI на GitHub...");

            var jsonString = await _httpClient.GetStringAsync(UiApiUrl);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            string tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";
            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            var currentVersion = ZapretVersion.Parse(UpdateResult.GetCurrentUiVersion());
            var latestVersion = ZapretVersion.Parse(tagName);

            bool hasUpdate = latestVersion > currentVersion;
            string? downloadUrl = null;

            if (hasUpdate && root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                        (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                         name.Contains("ZapretReborn", StringComparison.OrdinalIgnoreCase)))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            return new UpdateResult
            {
                HasUpdate = hasUpdate,
                LatestVersion = tagName,
                ReleaseNotes = body,
                DownloadUrl = downloadUrl
            };
        }
        catch (Exception ex)
        {
            progress?.Report($"Ошибка проверки UI: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Ошибка проверки UI обновлений: {ex.Message}");
            return new UpdateResult
            {
                HasUpdate = false,
                LatestVersion = UpdateResult.GetCurrentUiVersion(),
                ReleaseNotes = $"Ошибка проверки обновлений: {ex.Message}",
                DownloadUrl = null
            };
        }
    }

    /// <summary>
    /// Проверяет обновления скриптов через GitHub API (Flowseal)
    /// </summary>
    public static async Task<ScriptUpdateResult> CheckScriptUpdatesAsync()
    {
        try
        {
            var jsonString = await _httpClient.GetStringAsync(ScriptApiUrl);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            string tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";

            var currentVersion = ZapretVersion.Parse(AppPaths.GetCurrentScriptVersion());
            var latestVersion = ZapretVersion.Parse(tagName);

            bool hasUpdate = latestVersion > currentVersion;
            string? downloadUrl = null;

            if (hasUpdate && root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            return new ScriptUpdateResult
            {
                HasUpdate = hasUpdate,
                LatestVersion = tagName,
                DownloadUrl = downloadUrl
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений скриптов: {ex.Message}");
            return new ScriptUpdateResult
            {
                HasUpdate = false,
                LatestVersion = AppConstants.CurrentScriptVersion,
                DownloadUrl = null
            };
        }
    }

    /// <summary>
    /// Скачивает файл обновления с прогрессом
    /// </summary>
    public static async Task<bool> DownloadUpdateAsync(string downloadUrl, string destinationPath, IProgress<double>? progress = null)
    {
        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;

                if (totalBytes.HasValue && progress != null)
                {
                    double percentage = (double)totalRead / totalBytes.Value * 100;
                    progress.Report(percentage);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка скачивания обновления: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Устанавливает обновление UI приложения
    /// </summary>
    public static async Task<bool> InstallUiUpdateAsync(string updateFilePath, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Подготовка к установке UI...");

            if (!File.Exists(updateFilePath))
            {
                progress?.Report("Ошибка: файл обновления не найден");
                return false;
            }

            // Если это .zip архив - распаковываем
            if (updateFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Распаковка архива...");

                string tempDir = Path.Combine(Path.GetTempPath(), "ZapretReborn_Update");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                System.IO.Compression.ZipFile.ExtractToDirectory(updateFilePath, tempDir);

                // Ищем exe файл в распакованном архиве
                var exeFiles = Directory.GetFiles(tempDir, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length == 0)
                {
                    progress?.Report("Ошибка: в архиве не найден exe файл");
                    return false;
                }

                string newExePath = exeFiles[0];
                string currentExePath = Assembly.GetExecutingAssembly().Location;

                progress?.Report("Копирование файлов...");

                // Копируем новый exe файл вместо старого
                File.Copy(newExePath, currentExePath, true);

                // Очищаем временные файлы
                Directory.Delete(tempDir, true);
                File.Delete(updateFilePath);

                progress?.Report("UI обновление установлено!");
                return true;
            }
            // Если это exe файл напрямую
            else if (updateFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Замена executable файла...");

                string currentExePath = Assembly.GetExecutingAssembly().Location;

                if (!Path.GetFileName(updateFilePath).Equals("ZapretReborn.exe", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report("Ошибка: неверный файл обновления");
                    return false;
                }

                File.Copy(updateFilePath, currentExePath, true);
                File.Delete(updateFilePath);

                progress?.Report("UI обновление установлено!");
                return true;
            }
            else
            {
                progress?.Report("Ошибка: неподдерживаемый формат файла");
                return false;
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Ошибка установки: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Ошибка установки обновления: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Устанавливает обновление скриптов (вызывает ZapretDownloader)
    /// </summary>
    public static async Task<bool> InstallScriptUpdateAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Скачивание скриптов Zapret...");

            bool success = await ZapretDownloader.DownloadAndExtractLatestAsync((status, percent) =>
            {
                if (percent.HasValue)
                {
                    progress?.Report($"{status} ({Math.Round(percent.Value)}%)");
                }
                else
                {
                    progress?.Report(status);
                }
            });

            if (success)
            {
                progress?.Report("Скрипты успешно обновлены!");
                // Сбрасываем кэш путей
                AppPaths.ResetCache();
            }

            return success;
        }
        catch (Exception ex)
        {
            progress?.Report($"Ошибка установки скриптов: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Ошибка установки скриптов: {ex.Message}");
            return false;
        }
    }
}
