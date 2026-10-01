using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
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
                var releaseAssets = assets.EnumerateArray().ToArray();
                foreach (var asset in releaseAssets)
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (string.Equals(name, "ZapretReborn.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (downloadUrl == null)
                {
                    foreach (var asset in releaseAssets)
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("ZapretReborn", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
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
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);

            var buffer = new byte[128 * 1024];
            long totalRead = 0;
            int read;
            double lastReportedPercentage = 0;
            var progressTimer = Stopwatch.StartNew();

            if (totalBytes is > 0)
            {
                progress?.Report(0);
            }

            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;

                if (totalBytes is > 0 && progress != null)
                {
                    double percentage = (double)totalRead / totalBytes.Value * 100;
                    if (percentage - lastReportedPercentage >= 1 || progressTimer.ElapsedMilliseconds >= 250)
                    {
                        progress.Report(Math.Min(percentage, 100));
                        lastReportedPercentage = percentage;
                        progressTimer.Restart();
                    }
                }
            }

            if (totalBytes is > 0)
            {
                progress?.Report(100);
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
        string? updateDirectory = null;
        string? updaterScriptPath = null;
        bool updaterStarted = false;

        try
        {
            progress?.Report("Подготовка к установке UI...");

            if (!File.Exists(updateFilePath))
            {
                progress?.Report("Ошибка: файл обновления не найден.");
                return false;
            }

            updateDirectory = Path.Combine(Path.GetTempPath(), $"ZapretReborn_Update_{Guid.NewGuid():N}");
            string payloadDirectory = Path.Combine(updateDirectory, "payload");
            Directory.CreateDirectory(payloadDirectory);

            string payloadRoot;
            if (updateFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(Path.GetFileName(updateFilePath), "ZapretReborn.exe", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Ожидался single-file asset с именем ZapretReborn.exe.");
                }

                progress?.Report("Подготовка single-file обновления...");
                await Task.Run(() => File.Copy(updateFilePath, Path.Combine(payloadDirectory, "ZapretReborn.exe")));
                payloadRoot = payloadDirectory;
            }
            else if (updateFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Распаковка пакета приложения...");
                await Task.Run(() => ZipFile.ExtractToDirectory(updateFilePath, payloadDirectory));

                string? updatedExecutable = Directory
                    .GetFiles(payloadDirectory, "ZapretReborn.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (updatedExecutable == null)
                {
                    throw new InvalidDataException("В архиве не найден ZapretReborn.exe.");
                }

                payloadRoot = Path.GetDirectoryName(updatedExecutable)!;
            }
            else
            {
                throw new InvalidDataException("Неподдерживаемый формат обновления. Требуется ZapretReborn.exe или полный ZIP-пакет.");
            }

            updaterScriptPath = Path.Combine(Path.GetTempPath(), $"ZapretReborn_Updater_{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(updaterScriptPath, UiUpdateInstallerScript, new UTF8Encoding(false));

            string currentExecutable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь к запущенному приложению.");
            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            updaterStartInfo.ArgumentList.Add("-NoProfile");
            updaterStartInfo.ArgumentList.Add("-NonInteractive");
            updaterStartInfo.ArgumentList.Add("-ExecutionPolicy");
            updaterStartInfo.ArgumentList.Add("Bypass");
            updaterStartInfo.ArgumentList.Add("-File");
            updaterStartInfo.ArgumentList.Add(updaterScriptPath);
            updaterStartInfo.ArgumentList.Add("-ProcessId");
            updaterStartInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            updaterStartInfo.ArgumentList.Add("-InstallDirectory");
            updaterStartInfo.ArgumentList.Add(Path.GetDirectoryName(currentExecutable)!);
            updaterStartInfo.ArgumentList.Add("-PayloadDirectory");
            updaterStartInfo.ArgumentList.Add(payloadRoot);
            updaterStartInfo.ArgumentList.Add("-UpdateDirectory");
            updaterStartInfo.ArgumentList.Add(updateDirectory);
            updaterStartInfo.ArgumentList.Add("-ExecutableName");
            updaterStartInfo.ArgumentList.Add(Path.GetFileName(currentExecutable));

            if (Process.Start(updaterStartInfo) == null)
            {
                throw new InvalidOperationException("Не удалось запустить установщик обновления.");
            }
            updaterStarted = true;
            try
            {
                File.Delete(updateFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось удалить скачанный архив обновления: {ex.Message}");
            }

            progress?.Report("Обновление подготовлено. Закройте приложение, чтобы установить его.");
            return true;
        }
        catch (Exception ex)
        {
            if (!updaterStarted && updateDirectory != null && Directory.Exists(updateDirectory))
            {
                try
                {
                    Directory.Delete(updateDirectory, true);
                }
                catch (Exception cleanupException)
                {
                    System.Diagnostics.Debug.WriteLine($"Не удалось удалить временную папку обновления: {cleanupException.Message}");
                }
            }
            if (!updaterStarted && updaterScriptPath != null && File.Exists(updaterScriptPath))
            {
                try
                {
                    File.Delete(updaterScriptPath);
                }
                catch (Exception cleanupException)
                {
                    System.Diagnostics.Debug.WriteLine($"Не удалось удалить временный установщик: {cleanupException.Message}");
                }
            }
            progress?.Report($"Ошибка установки: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Ошибка установки обновления: {ex.Message}");
            return false;
        }
    }

    private const string UiUpdateInstallerScript = """
        param(
            [Parameter(Mandatory = $true)][int]$ProcessId,
            [Parameter(Mandatory = $true)][string]$InstallDirectory,
            [Parameter(Mandatory = $true)][string]$PayloadDirectory,
            [Parameter(Mandatory = $true)][string]$UpdateDirectory,
            [Parameter(Mandatory = $true)][string]$ExecutableName
        )

        $ErrorActionPreference = 'Stop'
        $logPath = Join-Path $UpdateDirectory 'updater.log'
        $backupDirectory = Join-Path $UpdateDirectory 'backup'
        $changedFiles = [System.Collections.Generic.List[string]]::new()
        $updateSucceeded = $false

        try {
            while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
                Start-Sleep -Milliseconds 500
            }

            $files = Get-ChildItem -LiteralPath $PayloadDirectory -File -Recurse | Where-Object {
                $relativePath = $_.FullName.Substring($PayloadDirectory.Length).TrimStart('\')
                $segments = $relativePath -split '[\\/]'
                $isZapretFolder = $segments[0].StartsWith('zapret-', [System.StringComparison]::OrdinalIgnoreCase)
                $isUserFile = $segments.Length -eq 1 -and $segments[0] -in @('config.ini', 'game_filter.enabled', '.patched')
                -not $isZapretFolder -and -not $isUserFile
            }

            if (-not ($files | Where-Object { $_.Name -ieq $ExecutableName })) {
                throw "Пакет обновления не содержит $ExecutableName."
            }

            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($PayloadDirectory.Length).TrimStart('\')
                $destination = Join-Path $InstallDirectory $relativePath
                $backup = Join-Path $backupDirectory $relativePath

                $destinationParent = Split-Path -Parent $destination
                if (-not (Test-Path -LiteralPath $destinationParent)) {
                    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
                }
                if (Test-Path -LiteralPath $destination) {
                    $backupParent = Split-Path -Parent $backup
                    if (-not (Test-Path -LiteralPath $backupParent)) {
                        New-Item -ItemType Directory -Path $backupParent -Force | Out-Null
                    }
                    Copy-Item -LiteralPath $destination -Destination $backup -Force
                }
                $changedFiles.Add($relativePath)
                Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            }

            $updatedExecutable = Join-Path $InstallDirectory $ExecutableName
            $newProcess = Start-Process -FilePath $updatedExecutable -PassThru
            Start-Sleep -Seconds 10
            if ($newProcess.HasExited) {
                throw "Обновлённое приложение завершилось при запуске."
            }

            $updateSucceeded = $true
        }
        catch {
            Add-Content -LiteralPath $logPath -Value $_.Exception.ToString()
            foreach ($relativePath in $changedFiles) {
                try {
                    $destination = Join-Path $InstallDirectory $relativePath
                    $backup = Join-Path $backupDirectory $relativePath
                    if (Test-Path -LiteralPath $backup) {
                        Copy-Item -LiteralPath $backup -Destination $destination -Force
                    }
                    elseif (Test-Path -LiteralPath $destination) {
                        Remove-Item -LiteralPath $destination -Force
                    }
                }
                catch {
                    Add-Content -LiteralPath $logPath -Value "Ошибка отката $relativePath : $($_.Exception.Message)"
                }
            }

            try {
                $oldExecutable = Join-Path $InstallDirectory $ExecutableName
                if (Test-Path -LiteralPath $oldExecutable) {
                    Start-Process -FilePath $oldExecutable
                }
                Add-Content -LiteralPath $logPath -Value 'Откат выполнен; приложение запущено с предыдущими файлами.'
            }
            catch {
                Add-Content -LiteralPath $logPath -Value "Не удалось запустить предыдущую версию: $($_.Exception.Message)"
            }
        }

        if ($updateSucceeded) {
            Remove-Item -LiteralPath $UpdateDirectory -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
        }
        """;

    /// <summary>
    /// Устанавливает обновление скриптов (вызывает ZapretDownloader)
    /// </summary>
    public static async Task<bool> InstallScriptUpdateAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Скачивание скриптов Zapret...");

            var result = await ZapretDownloader.DownloadAndExtractLatestAsync((status, percent) =>
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

            if (result.Success)
            {
                progress?.Report("Скрипты успешно обновлены!");
                // Сбрасываем кэш путей
                AppPaths.ResetCache();
            }
            else
            {
                progress?.Report($"Ошибка при загрузке: {result.ErrorMessage}");
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            progress?.Report($"Ошибка установки скриптов: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Ошибка установки скриптов: {ex.Message}");
            return false;
        }
    }
}
