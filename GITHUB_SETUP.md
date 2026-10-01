# ⚡ Быстрая настройка GitHub для ZapretReborn

Этот файл содержит пошаговую инструкцию для публикации вашего приложения на GitHub.

## 🎯 Чек-лист перед публикацией

- [ ] Приложение собирается без ошибок
- [ ] Версия в `AppConstants.cs` установлена на `2.0.0`
- [ ] Версия в `.csproj` установлена на `2.0.0`
- [ ] README.md создан и заполнен
- [ ] .gitignore настроен

## 🚀 Пошаговая инструкция

### Шаг 1: Создайте репозиторий на GitHub

1. Перейдите на [https://github.com/new](https://github.com/new)
2. Введите:
   - **Repository name**: `ZapretReborn`
   - **Description**: Удобный интерфейс для управления скриптами Zapret с автоматическими обновлениями
   - **Public/Private**: **Public** (важно для работы обновлений!)
   - **Initialize with README**: Не выбирайте (у нас уже есть README.md)
   - **Add .gitignore**: Выберите "VisualStudio, Windows"
   - **License**: MIT License

### Шаг 2: Настройте Git в проекте

Откройте командную строку в папке проекта и выполните:

```bash
# Инициализация Git
cd C:\Users\fedko\source\repos\ZapretReborn\ZapretReborn
git init

# Добавьте все файлы
git add .

# Создайте первый коммит
git commit -m "Initial commit - ZapretReborn v2.0.0"

# Создайте ветку main (если еще не создана)
git branch -M main

# Добавьте удаленный репозиторий (замените USERNAME на ваш)
git remote add origin https://github.com/USERNAME/ZapretReborn.git

# Отправьте код на GitHub
git push -u origin main
```

### Шаг 3: Настройте константы в коде

Откройте `AppConstants.cs` и измените:

```csharp
// GitHub API для обновлений UI приложения
public const string GitHubUiOwner = "ВАШ_USERNAME";  // ← Измените на свой GitHub username
public const string GitHubUiRepo = "ZapretReborn";     // ← Имя вашего репозитория
```

### Шаг 4: Создайте первый релиз

1. Перейдите на GitHub в ваш репозиторий
2. Нажмите **"Releases"** → **"Draft a new release"**
3. Заполните:
   - **Tag version**: `v2.0.0` (обязательно с буквой "v"!)
   - **Release title**: `ZapretReborn 2.0.0 - Initial Release`
   - **Description**: 
     ```
     Первая стабильная версия ZapretReborn с:
     - Графическим интерфейсом
     - Системой автоматических обновлений
     - Поддержкой тем
     - Автозагрузкой
     ```

4. **Прикрепите файлы:**
   - Соберите приложение в Release конфигурации
   - Заархивируйте папку `bin\Release\net8.0-windows10.0.19041.0\win-x64` в ZIP
   - Прикрепите архив к релизу

5. Нажмите **"Publish release"**

## 🔧 Проверка работы обновлений

### Тест обновления UI:

1. В коде измените `AppConstants.CurrentUiVersion` на `v1.0.0` (для теста)
2. Создайте новый релиз с версией `v2.0.1` на GitHub
3. Запустите приложение
4. Дождитесь проверки обновлений (или нажмите "Проверить обновления" в настройках)
5. Должно появиться уведомление о доступном обновлении

### Тест обновления скриптов:

1. Проверьте, что репозиторий [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) имеет актуальные релизы
2. В приложении перейдите в настройки
3. Нажмите "Проверить обновления"
4. Если есть новая версия скриптов, она будет предложена к установке

## 📁 Структура репозитория

```
ZapretReborn/
├── .gitignore              # Игнорируемые файлы
├── README.md               # Описание проекта
├── DEPLOYMENT.md           # Инструкция по развертыванию
├── GITHUB_SETUP.md         # Эта инструкция
├── ZapretReborn.sln       # Решение Visual Studio
├── ZapretReborn/          # Проект приложения
│   ├── App.xaml.cs         # Точка входа
│   ├── AppConstants.cs     # Константы (в т.ч. GitHub URLs)
│   ├── MainWindow.xaml     # Главное окно
│   ├── SettingsPage.xaml   # Страница настроек
│   ├── UpdateService.cs    # Сервис обновлений
│   ├── ZapretDownloader.cs # Загрузчик скриптов
│   └── ...                 # Другие файлы
└── Assets/                 # Ресурсы (иконки и т.д.)
```

## ⚙️ Настройка автоматизации (опционально)

### GitHub Actions для автоматической сборки

Создайте файл `.github/workflows/build.yml`:

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
    
    - name: Create ZIP
      run: Compress-Archive -Path "ZapretReborn\bin\Release\net8.0-windows10.0.19041.0\win-x64\*" -DestinationPath "ZapretReborn-${{ github.ref_name }}.zip"
    
    - name: Upload Release Asset
      uses: actions/upload-release-asset@v1
      with:
        upload_url: ${{ github.event.release.upload_url }}
        asset_path: ./ZapretReborn-${{ github.ref_name }}.zip
        asset_name: ZapretReborn-${{ github.ref_name }}.zip
        asset_content_type: application/zip
```

### Как это работает:

1. При создании тега с префиксом `v` (например, `v2.0.0`)
2. GitHub Actions автоматически:
   - Собирает проект
   - Создает self-containedexe
   - Архивирует в ZIP
   - Загружает как asset к релизу

## 🎯 Выпуск обновлений

### Для обновления UI:
1. Измените `AppConstants.CurrentUiVersion` на новую версию
2. Создайте коммит с изменениями
3. Создайте новый тег: `git tag v2.0.1`
4. Отправьте тег на GitHub: `git push origin v2.0.1`
5. GitHub автоматически создаст релиз (если настроен Actions)

### Для обновления скриптов:
- Скрипты обновляются из репозитория Flowseal
- Вам не нужно ничего делать, если Flowseal обновляет свой репозиторий

## 💡 Советы

1. **Тестируйте обновления на тестовой машине** перед релизами
2. **Используйте семантическое версионирование** (v2.0.0, v2.0.1, v2.1.0, v3.0.0)
3. **Держите README.md актуальным** с последними изменениями
4. **Мониторьте Issues** на GitHub для отзывов пользователей

## 🆘 Решение проблем

### Приложение не находит обновления
- Проверьте, что теги версий имеют формат `vX.Y.Z`
- Убедитесь, что репозиторий Public
- Проверьте подключение к GitHub API

### Ошибки при установке обновлений
- Проверьте права доступа к папке установки
- Убедитесь, что приложение не запущено во время обновления
- Проверьте, что ZIP-архив корректно упакован

### GitHub Actions не работает
- Проверьте синтаксис workflow файла
- Убедитесь, что в репозитории есть папка `.github/workflows/`
- Проверьте логи в GitHub Actions

## 📞 Поддержка

Если у вас возникли вопросы по настройке, свяжитесь с разработчиком.