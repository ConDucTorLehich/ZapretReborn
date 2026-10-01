# 🚀 Настройка публикации и обновлений

Этот файл описывает, как настроить публикацию приложения на GitHub и обеспечить работу системы обновлений.

## 📦 Публикация на GitHub

### 1. Создание репозитория

1. Перейдите на [GitHub.com](https://github.com) и войдите в аккаунт
2. Нажмите **"New repository"**
3. Укажите:
   - **Repository name**: `ZapretReborn`
   - **Description**: Удобный интерфейс для управления скриптами Zapret
   - **Public/Private**: Public (для доступа пользователей)
   - **Add .gitignore**: Windows, Visual Studio
   - **License**: MIT License
4. Нажмите **"Create repository"**

### 2. Загрузка кода

```bash
# Клонируйте созданный репозиторий
cd C:\Users\fedko\source\repos\ZapretReborn
git init
git add .
git commit -m "Initial commit - ZapretReborn 2.0.0"
git branch -M main
git remote add origin https://github.com/ВАШ_USERNAME/ZapretReborn.git
git push -u origin main
```

### 3. Настройка releases

#### Для обновлений UI приложения:

1. В вашем репозитории перейдите на вкладку **Releases**
2. Нажмите **"Draft a new release"**
3. Укажите:
   - **Tag version**: `v2.0.0` (пример)
   - **Release title**: `ZapretReborn 2.0.0`
   - **Description**: Описание изменений
4. Загрузите собранный `ZapretReborn.exe` или ZIP-архив с приложением
5. Нажмите **"Publish release"**

#### Для обновлений скриптов:
- Скрипты загружаются из репозитория [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube)
- Убедитесь, что этот репозиторий имеет актуальные релизы с ZIP-архивами

## ⚙️ Настройка системы обновлений

### Конфигурация в коде

Откройте `AppConstants.cs` и убедитесь, что:

```csharp
// GitHub API для обновлений UI приложения
public const string GitHubUiOwner = "ВАШ_USERNAME";      // Измените на свой
public const string GitHubUiRepo = "ZapretReborn";        // Имя вашего репозитория
public const string GitHubUiApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";

// GitHub API для обновлений скриптов
public const string GitHubScriptOwner = "Flowseal";
public const string GitHubScriptRepo = "zapret-discord-youtube";
```

### Важно!
- Убедитесь, что теги версий в релизах соответствуют формату `vX.Y.Z` (например, `v2.0.0`)
- Скрипты должны быть упакованы в ZIP-архив для автоматической загрузки
- UI обновления могут быть в формате .exe или .zip

## 🏗️ Сборка приложения

### Through Visual Studio
1. Откройте решение `ZapretReborn.sln` в Visual Studio 2022
2. Выберите конфигурацию **Release**
3. Выберите платформу **x64**
4. Соберите решение (**Build > Build Solution**)
5. Файл будет в: `bin\Release\net8.0-windows10.0.19041.0\win-x64\ZapretReborn.exe`

### Through Command Line
```bash
cd ZapretReborn
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

## 📋 Структура релиза

Каждый релиз должен содержать:

### Для UI обновлений:
- `ZapretReborn.exe` - основной файл приложения
- ИЛИ `ZapretReborn.zip` - архив со всем приложением

### Для скриптов (в репозитории Flowseal):
- `zapret-vX.Y.Z.zip` - архив со скриптами
- Внутри архива должна быть папка `zapret-X.Y.Z` с:
  - `service.bat`
  - Другими скриптами

## 🔄 Как работает система обновлений

### Проверка обновлений UI:
1. Приложение запрашивает GitHub API: `https://api.github.com/repos/OWNER/REPO/releases/latest`
2. Получает информацию о последнем релизе
3. Сравнивает `tag_name` с текущей версией (`AppConstants.CurrentUiVersion`)
4. Если новая версия доступна, предлагает скачать и установить

### Проверка обновлений скриптов:
1. Аналогично UI, но из репозитория Flowseal
2. Скачивает и распаковывает ZIP-архив
3. Удаляет старые скрипты перед установкой новых
4. Проверяет, что скрипты не запущены перед удалением

## 🛡️ Тестирование обновлений

1. Создайте тестовый релиз с версией выше текущей (например, `v2.0.1`)
2. Запустите приложение
3. Дождитесь автоматически проверки или нажмите "Проверить обновления" в настройках
4. Убедитесь, что:
   - Обнаружено новое обновление
   - Корректно отображается информация о версии
   - Обновление скачивается и устанавливается

## 📊 Версионирование

Следуйте семантическому версионированию:
- **Major** (X): Крупные изменения, несовместимость
- **Minor** (Y): Новые функции, совместимые изменения  
- **Patch** (Z): Исправления багов

Пример: `v2.0.0` → `v2.0.1` → `v2.1.0` → `v3.0.0`

## 🎯 BEST PRACTICES

1. **Регулярные релизы**: Публикуйте обновления регулярно
2. **Подробные описания**: Указывайте изменения в release notes
3. **Тестирование**: Проверяйте обновления перед публикацией
4. **Резервные копии**: Сохраняйте старые версии на случай проблем

## ❓ Решение проблем

### Обновления не находятся
- Проверьте подключение к интернету
- Убедитесь, что GitHub API доступен
- Проверьте, что теги версий корректны

### Ошибки установки
- Проверьте права доступа к папке
- Убедитесь, что файлы не используются другими процессами
- Проверьте формат ZIP-архива

### Приложение крашится после обновления
- Убедитесь, что все необходимые файлы включены в релиз
- Проверьте зависимости (.NET 8.0 Runtime)