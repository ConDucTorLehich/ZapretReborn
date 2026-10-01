# 🚀 Быстрый старт: Публикация на GitHub

Следуйте этим шагам, чтобы опубликовать ZapretReborn на GitHub и настроить работу обновлений.

## ⚡ Шаг 1: Подготовьте код (5 минут)

### 1.1 Обновите константы в коде

Откройте `AppConstants.cs` и измените:

```csharp
// Измените на свой GitHub username
public const string GitHubUiOwner = "ВАШ_GITHUB_USERNAME";

// Имя репозитория ( должно быть ZapretReborn)
public const string GitHubUiRepo = "ZapretReborn";
```

> ⚠️ **ВАЖНО**: GitHub username должен быть правильным, иначе обновления работать не будут!

### 1.2 Проверьте версию приложения

Убедитесь, что в `AppConstants.cs`:
```csharp
public const string CurrentUiVersion = "2.0.0";
public const string CurrentScriptVersion = "2.0.0";
```

И в `ZapretReborn.csproj`:
```xml
<AssemblyVersion>2.0.0</AssemblyVersion>
<FileVersion>2.0.0</FileVersion>
```

## ⚡ Шаг 2: Создайте репозиторий на GitHub (2 минуты)

1. Перейдите на [https://github.com/new](https://github.com/new)
2. Введите:
   - **Repository name**: `ZapretReborn`
   - **Description**: Удобный интерфейс для управления скриптами Zapret
   - **Public/Private**: **Public** ⚠️ (ОБЯЗАТЕЛЬНО для работы обновлений!)
   - **License**: MIT License
3. Нажмите **"Create repository"**

## ⚡ Шаг 3: Загрузите код (3 минуты)

### Вариант A: Через командную строку (рекомендуется)

```bash
cd C:\Users\fedko\source\repos\ZapretReborn\ZapretReborn

# Инициализация Git
git init
git add .
git commit -m "Initial commit - ZapretReborn v2.0.0"
git branch -M main

# Добавьте удаленный репозиторий (замените USERNAME на свой)
git remote add origin https://github.com/USERNAME/ZapretReborn.git

# Отправьте код на GitHub
git push -u origin main
```

### Вариант B: Через GitHub Desktop

1. Установите [GitHub Desktop](https://desktop.github.com/)
2. Откройте GitHub Desktop
3. **File > Add Local Repository**
4. Выберите папку `ZapretReborn`
5. Нажмите **"Publish Repository"**

## ⚡ Шаг 4: Создайте первый релиз (5 минут)

### 4.1 Соберите приложение

Через Visual Studio:
1. Откройте `ZapretReborn.sln`
2. Выберите **Release** конфигурацию
3. Выберите **x64** платформу
4. Соберите (**Build > Build Solution**)

ИЛИ через командную строку:
```bash
cd C:\Users\fedko\source\repos\ZapretReborn\ZapretReborn
dotnet build --configuration Release
```

### 4.2 Создайте ZIP-архив

```powershell
# Перейдите в папку Release
cd bin\Release\net8.0-windows10.0.19041.0\win-x64

# Создайте ZIP-архив
Compress-Archive -Path * -DestinationPath ..\..\..\..\ZapretReborn-v2.0.0.zip
```

### 4.3 Опубликуйте релиз на GitHub

1. Перейдите в свой репозиторий на GitHub
2. Нажмите **"Releases"** → **"Draft a new release"**
3. Введите:
   - **Tag version**: `v2.0.0` ⚠️ (ОБЯЗАТЕЛЬНО с буквой "v"!)
   - **Release title**: `ZapretReborn 2.0.0`
   - **Description**: 
     ```
     Первая стабильная версия ZapretReborn
     
     ## Что нового:
     - Графический интерфейс для управления скриптами
     - Автоматическая система обновлений
     - Поддержка тем (светлая/темная/системная)
     - Автозагрузка с Windows
     - Проверка запущенных процессов перед обновлением
     
     ## Установка:
     1. Скачайте ZIP-архив
     2. Разархивируйте в любую папку
     3. Запустите ZapretReborn.exe
     ```

4. **Прикрепите ZIP-файл**: Загрузите `ZapretReborn-v2.0.0.zip`
5. Нажмите **"Publish release"**

## ⚡ Шаг 5: Проверьте работу обновлений (2 минуты)

### 5.1 Проверьте обновление UI

1. В коде временно измените версию на старую:
   ```csharp
   // В AppConstants.cs
   public const string CurrentUiVersion = "1.0.0";  // Вместо 2.0.0
   ```

2. Соберите и запустите приложение
3. Дождитесь автоматически проверки обновлений (5 минут)
   ИЛИ
   Откройте настройки → нажмите "Проверить обновления"

4. Должно появиться уведомление: **"Найдено обновление UI: v2.0.0"**

5. После обновления верните версию обратно на `2.0.0`

### 5.2 Проверьте обновление скриптов

1. Убедитесь, что репозиторий [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) имеет актуальные релизы
2. Откройте настройки → "Проверить обновления"
3. Если есть новая версия скриптов, она будет предложена к установке

## ✅ Чек-лист готовности

- [ ] Код загружен на GitHub в Public репозиторий
- [ ] AppConstants.cs содержит правильный GitHub username
- [ ] Создан релиз с тегом `v2.0.0`
- [ ] ZIP-архив прикреплен к релизу
- [ ] Приложение собирается без ошибок
- [ ] Система обновлений работает (проверено)
- [ ] README.md заполнен

## 📋 Что у вас должно получиться

### Структура GitHub репозитория:
```
ZapretReborn/
├── .gitignore
├── README.md
├── DEPLOYMENT.md
├── GITHUB_SETUP.md
├── QUICK_START.md
├── ZapretReborn.sln
├── ZapretReborn/
│   ├── App.xaml.cs
│   ├── AppConstants.cs
│   └── ... (остальные файлы)
└── Assets/
```

### Структура релиза:
- Тег: `v2.0.0`
- Title: `ZapretReborn 2.0.0`
- Asset: `ZapretReborn-v2.0.0.zip`

## 🎯 Следующие шаги

### Для обновления приложения:

1. Измените версию в `AppConstants.cs`:
   ```csharp
   public const string CurrentUiVersion = "2.0.1";
   ```

2. Создайте коммит:
   ```bash
   git add .
   git commit -m "Update to v2.0.1"
   git tag v2.0.1
   git push origin main --tags
   ```

3. Создайте новый релиз на GitHub с тегом `v2.0.1`

### Для пользователей:

Пользователи смогут:
- Скачать последнюю версию из раздела Releases
- Получать автоматическое уведомление об обновлениях
- Обновляться одним кликом

## 🔧 Полезные команды

```bash
# Сборка
dotnet build --configuration Release

# Публикация
dotnet publish --configuration Release --runtime win-x64 --self-contained true

# Проверка статуса Git
git status

# Создание тега
git tag v2.0.0 -m "Release v2.0.0"

# Отправка на GitHub
git push origin main --tags
```

## ❓ Вопросы и ответы

### Q: Почему репозиторий должен быть Public?
**A**: GitHub API для проверки релизов требует Public доступа. Private репозитории не работают с текущей системой обновлений.

### Q: Как часто проверяются обновления?
**A**: Каждые 5 минут автоматически, плюс при открытии настроек.

### Q: Можно ли изменить интервал проверки?
**A**: Да, измените `UpdateCheckInterval` в `AppConstants.cs`.

### Q: Что если GitHub API недоступен?
**A**: Приложение продолжит работать с текущей версией и попробует позже.

## 📞 Поддержка

Если у вас возникли проблемы с настройкой, проверьте:
1. Правильность GitHub username в `AppConstants.cs`
2. Что репозиторий Public
3. Что теги версий имеют формат `vX.Y.Z`
4. Что ZIP-архив корректно упакован

Все необходимое для публикации вы найдете в файлах:
- `README.md` - основная информация
- `DEPLOYMENT.md` - детальная настройка
- `GITHUB_SETUP.md` - инструкция по GitHub
- `publish_to_github.ps1` - скрипт для автоматизации

Готово! 🎉 Ваше приложение veröffentlicht и готово к использованию.