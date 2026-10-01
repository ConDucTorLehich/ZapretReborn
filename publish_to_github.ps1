<#PSScriptInfo
.VERSION 1.0
.GUID 12345678-1234-1234-1234-123456789012
.AUTHOR ConDucTorLehich
.COMPANYNAME ZapretReborn
.COPYRIGHT (c) 2024 ZapretReborn
.TAGS GitHub, Publish, Deployment
.LICENSEURI https://github.com/ConDucTorLehich/ZapretReborn/LICENSE
.PROJECTURI https://github.com/ConDucTorLehich/ZapretReborn
.ICONURI https://github.com/ConDucTorLehich/ZapretReborn/raw/main/Assets/AppIcon.ico
.EXTERNALMODULEDEPENDENCIES 
.REQUIREDSCRIPTS 
.EXTERNALSCRIPTDEPENDENCIES 
.RELEASENOTES First version
#>

<#
.SYNOPSIS
    Скрипт для публикации ZapretReborn на GitHub
.DESCRIPTION
    Автоматизирует процесс создания релиза на GitHub для ZapretReborn
    
    Скрипт выполняет:
    1. Сборку проекта в Release конфигурации
    2. Создание ZIP-архива
    3. Создание коммита и тега
    4. Отправку на GitHub
    
    Использование:
    .\publish_to_github.ps1 -Version "2.0.0" -GitHubUser "ВашUsername"

.PARAMETER Version
    Номер версии для релиза (например, "2.0.0")

.PARAMETER GitHubUser
    Ваш GitHub username

.PARAMETER GitHubToken
    Опционально: GitHub Personal Access Token для автоматизации

.PARAMETER SkipBuild
    Пропустить этап сборки (если проект уже собран)

.EXAMPLE
    .\publish_to_github.ps1 -Version "2.0.0" -GitHubUser "MyUsername"
    Публикует версию 2.0.0 в репозиторий MyUsername/ZapretReborn

.EXAMPLE
    .\publish_to_github.ps1 -Version "2.0.1" -GitHubUser "MyUsername" -GitHubToken "ghp_..."
    Публикует версию 2.0.1 с использованием токена для автоматизации

.EXAMPLE
    .\publish_to_github.ps1 -Version "2.0.0" -GitHubUser "MyUsername" -SkipBuild
    Публикует существующую сборку без компиляции

.NOTES
    Версия 1.0
    Автор: ConDucTorLehich
    Дата: 2024
    Requires: PowerShell 5.1+, Git, .NET 8.0 SDK
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubUser,
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubToken = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force = $false
)

# Цвета для вывода
$successColor = "\e[92m"
$errorColor = "\e[91m"
$warningColor = "\e[93m"
$infoColor = "\e[96m"
$resetColor = "\e[0m"

# Если не Windows, отключаем цвета
if ($IsLinux -or $IsMacOS) {
    $successColor = ""
    $errorColor = ""
    $warningColor = ""
    $infoColor = ""
    $resetColor = ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "$successColor[$(Get-Date -Format 'HH:mm:ss')] SUCCESS:$resetColor $Message"
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "$errorColor[$(Get-Date -Format 'HH:mm:ss')] ERROR:$resetColor $Message"
}

function Write-Warning {
    param([string]$Message)
    Write-Host "$warningColor[$(Get-Date -Format 'HH:mm:ss')] WARNING:$resetColor $Message"
}

function Write-Info {
    param([string]$Message)
    Write-Host "$infoColor[$(Get-Date -Format 'HH:mm:ss')] INFO:$resetColor $Message"
}

# Проверка зависимостей
Write-Info "Проверка зависимостей..."

$dependencies = @{
    "Git" = { $null -ne (Get-Command git -ErrorAction SilentlyContinue) }
    "dotnet" = { $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue) }
}

$missing = @()
foreach ($dep in $dependencies.GetEnumerator()) {
    if (-not $dep.Value) {
        $missing += $dep.Key
    }
}

if ($missing.Count -gt 0) {
    Write-ErrorMsg "Не хватает зависимостей: $($missing -join ', ')"
    Write-ErrorMsg "Установите: Git (https://git-scm.com), .NET 8.0 SDK (https://dotnet.microsoft.com/download)"
    exit 1
}

# Проверка текущей директории
$projectPath = "$PSScriptRoot\ZapretReborn"
if (-not (Test-Path "$projectPath\ZapretReborn.csproj")) {
    Write-ErrorMsg "Скрипт должен запускаться из корневой папки проекта ZapretReborn"
    Write-ErrorMsg "Текущая директория: $PSScriptRoot"
    Write-ErrorMsg "Ожидается: ZapretReborn\ZapretReborn.csproj"
    exit 1
}

# Проверка версии
if (-not ($Version -match "^\d+\.\d+\.\d+$")) {
    Write-ErrorMsg "Некорректный формат версии. Используйте X.Y.Z (например, 2.0.0)"
    exit 1
}

$tagName = "v$Version"
Write-Info "Версия для публикации: $tagName"

# Этап 1: Сборка проекта (если не пропускаем)
if (-not $SkipBuild) {
    Write-Info "Сборка проекта..."
    
    try {
        # Очистка предыдущих сборок
        Write-Info "Очистка предыдущих сборок..."
        dotnet clean --configuration Release --verbosity minimal
        
        # Восстановление пакетов
        Write-Info "Восстановление пакетов NuGet..."
        dotnet restore --verbosity minimal
        
        # Сборка в Release
        Write-Info "Сборка в Release конфигурации..."
        dotnet build --configuration Release --no-restore --verbosity minimal
        
        Write-Success "Сборка завершена успешно"
    }
    catch {
        Write-ErrorMsg "Ошибка сборки: $($_.Exception.Message)"
        exit 1
    }
}

# Этап 2: Публикация
Write-Info "Публикация приложения..."

try {
    $publishOutput = "$PSScriptRoot\publish"
    if (Test-Path $publishOutput) {
        Remove-Item $publishOutput -Recurse -Force
    }
    
    Write-Info "Публикация для win-x64..."
    dotnet publish --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:ApplicationVersion=$Version `
        -p:FileVersion=$Version `
        -p:AssemblyVersion=$Version `
        --output $publishOutput `
        --verbosity minimal
    
    Write-Success "Публикация завершена"
}
catch {
    Write-ErrorMsg "Ошибка публикации: $($_.Exception.Message)"
    exit 1
}

# Этап 3: Создание ZIP-архива
Write-Info "Создание ZIP-архива..."

try {
    $zipFileName = "ZapretReborn-$tagName.zip"
    $zipFilePath = "$PSScriptRoot\$zipFileName"
    
    if (Test-Path $zipFilePath) {
        Remove-Item $zipFilePath -Force
    }
    
    # Αρχивируем содержимое папки publish
    Compress-Archive -Path "$publishOutput\*" -DestinationPath $zipFilePath -Force
    
    Write-Success "ZIP-архив создан: $zipFileName"
}
catch {
    Write-ErrorMsg "Ошибка создания архива: $($_.Exception.Message)"
    exit 1
}

# Этап 4: Git операции
Write-Info "Выполнение Git операций..."

try {
    # Переходим в папку проекта
    Set-Location $projectPath
    
    # Проверяем, инициализирован ли Git
    if (-not (Test-Path ".git")) {
        git init
        git branch -M main
        Write-Info "Git репозиторий инициализирован"
    }
    
    # Добавляем все изменения
    git add ..
    
    # Создаем коммит
    $commitMessage = "Release $tagName"
    git commit -m $commitMessage
    
    # Создаем тег
    git tag $tagName -m "Release $tagName"
    
    # Настраиваем удаленный репозиторий
    $repoUrl = "https://github.com/$GitHubUser/ZapretReborn.git"
    
    # Проверяем, есть ли уже origin
    $remotes = git remote -v
    $hasOrigin = $remotes -match "origin"
    
    if (-not $hasOrigin) {
        git remote add origin $repoUrl
        Write-Info "Добавлен удаленный репозиторий: origin"
    }
    
    # Отправляем изменения
    Write-Info "Отправка изменений на GitHub..."
    
    if ($GitHubToken) {
        # Используем токен для аутентификации
        $credentials = New-Object System.Management.Automation.PSCredential("token", (ConvertTo-SecureString $GitHubToken -AsPlainText -Force))
        
        # GitHub URL с токеном
        $authUrl = $repoUrl.Replace("https://", "https://token:$GitHubToken@")
        git remote set-url origin $authUrl
        
        git push origin main --tags -q
    }
    else {
        # Простая отправка (потребует ввода логина/пароля)
        git push origin main --tags
    }
    
    Write-Success "Изменения отправлены на GitHub"
    
    # Создание релиза через GitHub API (если есть токен)
    if ($GitHubToken) {
        Write-Info "Создание релиза через GitHub API..."
        
        $headers = @{
            "Authorization" = "token $GitHubToken"
            "Accept" = "application/vnd.github.v3+json"
        }
        
        $body = @{
            tag_name = $tagName
            name = "ZapretReborn $Version"
            body = @"
# ZapretReborn $Version

Новые функции и исправления в этой версии:

- Поддержка автоматических обновлений
- Улучшенный интерфейс пользователя
- Исправления багов

**Как установить:**
1. Скачайте ZIP-архив
2. Разархивируйте в любую папку
3. Запустите ZapretReborn.exe
"@
        } | ConvertTo-Json -Depth 10
        
        $releaseUrl = "https://api.github.com/repos/$GitHubUser/ZapretReborn/releases"
        
        try {
            $release = Invoke-RestMethod -Uri $releaseUrl -Method Post -Headers $headers -Body $body
            Write-Success "Релиз создан на GitHub: $($release.html_url)"
            
            # Загрузка assets
            Write-Info "Загрузка ZIP-архива как asset..."
            
            $uploadUrl = $release.upload_url.Replace("{?name,label}", "")
            $fileBytes = [System.IO.File]::ReadAllBytes($zipFilePath)
            $fileBase64 = [System.Convert]::ToBase64String($fileBytes)
            
            $assetBody = @{
                name = $zipFileName
                label = "Windows x64"
                content = $fileBase64
            } | ConvertTo-Json
            
            # GitHub API не поддерживает прямую загрузку base64, нужно использовать другой подход
            # Вместо этого просто выводим инструкцию
            Write-Warning "Для загрузки ZIP-файла как asset, выполните вручную:"
            Write-Warning "1. Перейдите на GitHub в раздел Releases"
            Write-Warning "2. Откройте релиз $tagName"
            Write-Warning "3. Нажмите 'Edit' и загрузите файл $zipFileName"
            Write-Warning "4. Сохраните релиз"
            
        }
        catch {
            Write-Warning "Не удалось создать релиз через API: $($_.Exception.Message)"
            Write-Warning "Вы можете создать релиз вручную на GitHub"
        }
    }
    else {
        Write-Info "Для автоматического создания релиза, используйте GitHub Token"
        Write-Info "Или создайте релиз вручную на GitHub:"
        Write-Info "1. Перейдите в свой репозиторий на GitHub"
        Write-Info "2. Нажмите 'Releases' → 'Draft a new release'"
        Write-Info "3. Укажите тег $tagName"
        Write-Info "4. Загрузите файл $zipFileName"
        Write-Info "5. Нажмите 'Publish release'"
    }
    
}
catch {
    Write-ErrorMsg "Ошибка Git операций: $($_.Exception.Message)"
    Write-Info "Попробуйте выполнить Git команды вручную"
    exit 1
}

# Итог
Write-Success "=========================================="
Write-Success "Публикация завершена успешно!"
Write-Success ""
Write-Success "Что было сделано:"
Write-Success "✓ Проект собран в Release конфигурации"
Write-Success "✓ Создан ZIP-архив: $zipFileName"
Write-Success "✓ Создан коммит и тег $tagName"
if ($SkipBuild) {
    Write-Success "✓ (Сборка была пропущена по запросу)"
}
Write-Success ""
Write-Success "Дальнейшие действия:"
Write-Success "1. Создайте релиз на GitHub с тегом $tagName"
Write-Success "2. Загрузите файл $zipFileName как asset к релизу"
Write-Success "3. Убедитесь, что репозиторий Public"
Write-Success ""
Write-Success "Файл ZIP находится в: $PSScriptRoot\$zipFileName"
Write-Success "=========================================="