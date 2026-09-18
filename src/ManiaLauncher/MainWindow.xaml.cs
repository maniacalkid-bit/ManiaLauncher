using System.Windows;
using System.Windows.Controls;
using ManiaLauncher.Core.Auth;
using ManiaLauncher.Core.Logging;
using ManiaLauncher.Core.Models;
using ManiaLauncher.Core.Services;
using ManiaLauncher.Core.Utils;

namespace ManiaLauncher;

public partial class MainWindow : Window
{
    private readonly Logger _logger = new();
    private readonly AccountService _accounts;
    private readonly ProfileService _profiles;
    private readonly VersionService _versions;
    private readonly JavaService _java;
    private readonly LaunchService _launcher;
    private readonly OfflineAuthService _offlineAuth = new();

    public MainWindow()
    {
        InitializeComponent();

        Paths.EnsureDirectories();

        _accounts = new AccountService(Paths.DataDirectory);
        _profiles = new ProfileService(Paths.DataDirectory);
        _versions = new VersionService(Paths.GameDirectory);
        _java = new JavaService(Paths.AppData);
        _launcher = new LaunchService(Paths.GameDirectory);

        _logger.OnLog += (level, msg) =>
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(msg + Environment.NewLine);
                LogBox.ScrollToEnd();
            });
        };

        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusText.Text = "Загрузка...";
            RefreshAccounts();
            RefreshProfiles();
            await RefreshVersionsAsync();
            StatusText.Text = "Готов";
            _logger.Info("Mania Launcher запущен");
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            StatusText.Text = "Ошибка при запуске";
        }
    }

    private void RefreshAccounts()
    {
        AccountsList.ItemsSource = null;
        AccountsList.ItemsSource = _accounts.Accounts;
    }

    private void RefreshProfiles()
    {
        ProfilesList.ItemsSource = null;
        // Показываем имя + версию
        ProfilesList.DisplayMemberPath = null;
        ProfilesList.Items.Clear();
        foreach (var p in _profiles.Profiles)
        {
            ProfilesList.Items.Add(p);
        }
        ProfilesList.DisplayMemberPath = "Name";
    }

    private async Task RefreshVersionsAsync()
    {
        try
        {
            StatusText.Text = "Загрузка списка версий...";
            var manifest = await _versions.GetVersionManifestAsync();
            VersionsList.ItemsSource = manifest.Versions
                .Where(v => v.Type == "release")
                .Take(50)
                .ToList();
            StatusText.Text = $"Загружено версий: {manifest.Versions.Count(v => v.Type == "release")}";
            _logger.Info($"Список версий обновлён ({manifest.Versions.Count} всего)");
        }
        catch (Exception ex)
        {
            _logger.Error("Не удалось загрузить версии: " + ex.Message);
            StatusText.Text = "Ошибка загрузки версий";
            MessageBox.Show(
                "Не удалось загрузить список версий.\n\n" +
                "Проверьте интернет-соединение.\n\n" +
                "Ошибка: " + ex.Message,
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RefreshVersions_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshVersionsAsync();
    }

    private async void InstallVersion_Click(object sender, RoutedEventArgs e)
    {
        if (VersionsList.SelectedItem is not MinecraftVersion version)
        {
            MessageBox.Show("Сначала выберите версию из списка", "Mania Launcher");
            return;
        }

        try
        {
            StatusText.Text = $"Установка {version.Id}...";
            _logger.Info($"Начинаю установку версии {version.Id}");

            var progress = new Progress<string>(msg =>
            {
                _logger.Info(msg);
                StatusText.Text = msg;
            });

            await _versions.DownloadVersionAsync(version, progress);

            _logger.Info($"Версия {version.Id} успешно установлена");
            StatusText.Text = $"{version.Id} установлена";
            MessageBox.Show($"Версия {version.Id} успешно установлена!\n\nТеперь создай профиль с этой версией.", "Готово");
            await RefreshVersionsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Ошибка установки: " + ex.Message);
            StatusText.Text = "Ошибка установки";
            MessageBox.Show(
                "Не удалось установить версию.\n\n" +
                "Ошибка: " + ex.Message,
                "Ошибка установки",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void AddOffline_Click(object sender, RoutedEventArgs e)
    {
        var username = OfflineUsernameBox.Text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Введите ник", "Mania Launcher");
            return;
        }

        try
        {
            var account = _offlineAuth.Login(username);
            _accounts.Add(account);
            RefreshAccounts();
            OfflineUsernameBox.Clear();
            _logger.Info($"Добавлен оффлайн-аккаунт: {username}");
            StatusText.Text = $"Добавлен оффлайн-аккаунт: {username}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }

    private void AddMicrosoft_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Вход через Microsoft пока находится в разработке.\n\n" +
            "Пока что используйте оффлайн-аккаунты.\n\n" +
            "Функция будет доступна в следующих обновлениях.",
            "Функция в разработке",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_accounts.Accounts.Count == 0)
        {
            MessageBox.Show("Сначала добавьте аккаунт во вкладке «Аккаунты»", "Mania Launcher");
            return;
        }

        if (VersionsList.SelectedItem is not MinecraftVersion selectedVersion)
        {
            MessageBox.Show(
                "Сначала выберите версию во вкладке «Версии».\n\n" +
                "Если версия ещё не установлена — установите её.",
                "Mania Launcher");
            return;
        }

        var account = _accounts.Accounts.First();
        var versionId = selectedVersion.Id;

        var profile = _profiles.Create($"Профиль {_profiles.Profiles.Count + 1} ({versionId})", versionId, account.Id);
        RefreshProfiles();
        ProfilesList.SelectedItem = profile;
        _logger.Info($"Создан профиль: {profile.Name} → версия {versionId}");
        StatusText.Text = $"Создан профиль: {profile.Name}";
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfilesList.SelectedItem is Profile p)
        {
            StatusText.Text = $"Профиль: {p.Name} | Версия: {p.VersionId}";
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not Profile profile)
        {
            MessageBox.Show("Выберите профиль слева", "Mania Launcher");
            return;
        }

        var account = _accounts.GetById(profile.AccountId);
        if (account == null)
        {
            MessageBox.Show("Аккаунт для этого профиля не найден.\nДобавьте аккаунт заново.", "Mania Launcher");
            return;
        }

        try
        {
            StatusText.Text = "Подготовка к запуску...";
            _logger.Info($"Запуск профиля «{profile.Name}» (версия {profile.VersionId})");

            var java = await _java.EnsureJavaAsync(new Progress<string>(m => _logger.Info(m)));

            var process = _launcher.Launch(profile, account, java, new Progress<string>(m => _logger.Info(m)));
            StatusText.Text = $"Игра запущена (PID {process.Id})";
            _logger.Info("Процесс Minecraft запущен");
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
            MessageBox.Show(ex.Message, "Ошибка запуска");
            StatusText.Text = "Ошибка запуска";
        }
    }
}
