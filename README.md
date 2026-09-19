# Mania Launcher

Открытый лаунчер Minecraft для Windows на C# / WPF (.NET 8).
Владелец и автор: **maniacalkid** · Лицензия: **MIT**

Монохромный дизайн: чёрный, тёмно-серый, светло-серый, белый — никаких цветных акцентов.

![Mania Launcher](assets/logo.png)

## Возможности

- **Автоматическое обновление версий.** Список версий загружается напрямую из официального манифеста Mojang (`version_manifest_v2.json`). Новые релизы и снапшоты появляются в лаунчере сразу после публикации; при обнаружении новой версии показывается уведомление.
- **Offline-аккаунты.** Создавайте локальные аккаунты для тестирования лаунчера. UUID генерируется по официальной схеме vanilla (`MD5("OfflinePlayer:" + имя)`), поэтому скины/плащи в offline-режиме не загружаются, а аккаунт существует только на серверах с `online-mode=false`.
- **Установка и запуск в один клик.** Кнопка PLAY сама скачивает клиент, ассеты и библиотеки (параллельная загрузка) и запускает игру с прогресс-баром.
- **Автоподбор Java.** Java 21 для 1.20.5+, Java 17 для 1.17+, Java 8 для старых версий; найденные в системе JDK перечисляются в настройках, можно указать свой путь.
- **Полный контроль настроек.** Папка игры, объём RAM (слайдер), поведение при запуске игры, фильтры версий (снапшоты, старые версии), поиск по версиям.
- **Живая консоль** с логом лаунчера и кнопка открытия лог-файлов.
- **Кастомное borderless-окно** с поддержкой Per-Monitor V2 DPI.

## Архитектура

```
src/ManiaLauncher/
├── App.xaml(.cs)            # точка входа, глобальные обработчики исключений
├── AppInfo.cs               # брендинг и пути (%APPDATA%\ManiaLauncher)
├── MainWindow.xaml(.cs)     # единственное окно: титулбар, сайдбар, страницы, футер с PLAY
├── Services/
│   ├── AccountService.cs    # offline-аккаунты, offline-UUID, accounts.json
│   ├── GameLauncherService.cs # обёртка над CmlLib.Core: установка + запуск
│   ├── JavaService.cs       # поиск JDK, выбор версии Java под версию игры
│   ├── LogService.cs        # rolling-лог (logs/launcher.log)
│   ├── SettingsService.cs   # settings.json (папка игры, RAM, окно и т.д.)
│   └── VersionService.cs    # манифест Mojang + локальные версии, детектор новых
├── ViewModels/              # MVVM: MainViewModel, ViewModelBase, RelayCommand
├── Converters/              # XAML-конвертеры
└── Themes/                  # Palette.xaml (палитра) + Controls.xaml (стили)
```

Движок — [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) 4.0.6 (MIT): манифест версий, проверка/распаковка ассетов и библиотек, сборка командной строки запуска.

### Задел под Microsoft-аккаунты

Сейчас реализована только offline-аутентификация, но архитектура позволяет добавить вход через Microsoft **без переписывания**:

- вся работа с сессиями изолирована в `GameLauncherService.InstallAndLaunchAsync`, где сейчас вызывается `MSession.CreateOfflineSession(username)` — достаточно подменить эту строку на сессию из OAuth-флоу (CmlLib поддерживает `MSession` с accessToken/uuid напрямую);
- `AccountService` хранит аккаунты в `accounts.json` и абстрагирован от способа входа;
- UI-страница Accounts уже работает со списком аккаунтов, а не с одиночным именем.

## Системные требования

- Windows 10/11 x64
- Java Runtime: **Java 21** (рекомендуется, для современных версий). Лаунчер сам найдёт установленные JDK; при отсутствии — подскажет, какую версию поставить. Для старых версий (до 1.16) нужна Java 8, для 1.17–1.20.4 — Java 17.
- ~5 ГБ свободного места под игровые файлы (ассеты, библиотеки, версии)

## Установка

### Установщик (рекомендуется)

`dist/ManiaLauncher-1.0.0-Setup-win-x64.exe` — обычная установка с ярлыками в меню Пуск и (опционально) на рабочем столе, деинсталлятором, выбором языка (EN/RU). Права администратора не требуются (установка per-user).

### Portable

`dist/ManiaLauncher-1.0.0-portable-win-x64.zip` — распакуйте в любую папку и запустите `ManiaLauncher.exe`. Сборка self-contained: **.NET Runtime устанавливать не нужно**.

Portable-режим данных: задайте переменную окружения `MANIA_DATA_DIR` — лаунчер будет хранить настройки, аккаунты, логи и игру в указанной папке вместо `%APPDATA%\ManiaLauncher`.

## Сборка из исходников

Требуется [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/maniacalkid/ManiaLauncher
cd ManiaLauncher
dotnet build src/ManiaLauncher/ManiaLauncher.csproj -c Release
```

Self-contained публикация и артефакты:

```bash
# portable-папка (publish/portable)
dotnet publish src/ManiaLauncher/ManiaLauncher.csproj -c Release -r win-x64 --self-contained true -o publish/portable

# ZIP (PowerShell)
Compress-Archive -Path publish/portable/* -DestinationPath dist/ManiaLauncher-1.0.0-portable-win-x64.zip

# установщик (нужен Inno Setup 6)
ISCC.exe installer/ManiaLauncher.iss
```

Скрипт установщика: `installer/ManiaLauncher.iss`. Иконка/логотип: `assets/app.ico`, `assets/logo.png`.

## Данные и файлы

| Что | Где |
|---|---|
| Настройки | `%APPDATA%\ManiaLauncher\settings.json` |
| Аккаунты | `%APPDATA%\ManiaLauncher\accounts.json` |
| Логи лаунчера | `%APPDATA%\ManiaLauncher\logs\launcher.log` |
| Игра (по умолчанию) | `%APPDATA%\ManiaLauncher\game` |

Папку игры можно сменить в настройках — например, на существующую `.minecraft`, чтобы переиспользовать уже скачанные файлы.

## Дорожная карта

- [x] Offline-аккаунты
- [x] Автообновление списка версий из манифеста Mojang
- [x] Параллельная установка клиентских файлов с прогрессом
- [x] Автоподбор Java под версию игры
- [x] Установщик (Inno Setup) + portable ZIP
- [ ] Вход через Microsoft (архитектура готова)
- [ ] Поддержка модлоадеров (Forge / Fabric / NeoForge)
- [ ] Профили (несколько сборок с разными настройками)
- [ ] Локализация UI

## Лицензия

[MIT](LICENSE) © 2026 maniacalkid

Minecraft является товарным знаком Mojang Synergies AB / Microsoft. Проект не аффилирован с Mojang и не использует их ресурсы, кроме общедоступных файлов игры и манифеста версий.
