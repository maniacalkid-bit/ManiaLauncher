using CmlLib.Core;
using ManiaLauncher.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ManiaLauncher.ViewModels;

public enum NavPage { Home, Versions, Accounts, Settings, Console }

public sealed class MainViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    public MainViewModel()
    {
        Accounts = AccountService.Instance.Accounts;
        SelectedAccount = AccountService.Instance.SelectedAccount;

        GameLauncherService.Instance.ProgressChanged += OnLaunchProgress;
        GameLauncherService.Instance.StatusChanged += s => App.Current.Dispatcher.Invoke(() => StatusText = s);
        VersionService.Instance.NewVersionDetected += OnNewVersionDetected;

        PlayCommand = new RelayCommand(async _ => await PlayAsync(), _ => CanPlay);
        RefreshVersionsCommand = new RelayCommand(async _ => await RefreshVersionsAsync(), _ => !IsBusy);
        AddAccountCommand = new RelayCommand(_ => AddAccount(), _ => !string.IsNullOrWhiteSpace(NewAccountName));
        RemoveAccountCommand = new RelayCommand(_ => RemoveAccount(), _ => SelectedAccount != null);
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        OpenGameDirCommand = new RelayCommand(_ => OpenFolder(SettingsService.Instance.GameDirectory));
        OpenLogsCommand = new RelayCommand(_ => OpenFolder(AppInfo.LogsDir));
        ResetSettingsCommand = new RelayCommand(_ => ResetSettings());

        LoadSettingsIntoUi();
        _ = InitializeAsync();
    }

    private void RaiseCanPlay()
    {
        OnPropertyChanged(nameof(CanPlay));
        // May be called from property setters during construction, before
        // commands are assigned.
        PlayCommand?.RaiseCanExecuteChanged();
    }

    // ================= State =================

    private NavPage _currentPage = NavPage.Home;
    public NavPage CurrentPage
    {
        get => _currentPage;
        set
        {
            if (Set(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsHomePage));
                OnPropertyChanged(nameof(IsVersionsPage));
                OnPropertyChanged(nameof(IsAccountsPage));
                OnPropertyChanged(nameof(IsSettingsPage));
                OnPropertyChanged(nameof(IsConsolePage));
            }
        }
    }

    public bool IsHomePage => CurrentPage == NavPage.Home;
    public bool IsVersionsPage => CurrentPage == NavPage.Versions;
    public bool IsAccountsPage => CurrentPage == NavPage.Accounts;
    public bool IsSettingsPage => CurrentPage == NavPage.Settings;
    public bool IsConsolePage => CurrentPage == NavPage.Console;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { Set(ref _isBusy, value); RaiseCanPlay(); }
    }

    private bool _canPlay = true;
    public bool CanPlay
    {
        get => _canPlay && !_isBusy && SelectedVersion != null && SelectedAccount != null;
        set => Set(ref _canPlay, value);
    }

    private string _statusText = "Loading…";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { Set(ref _progress, value); OnPropertyChanged(nameof(IsProgressVisible)); }
    }

    public bool IsProgressVisible => Progress > 0 && Progress < 1;

    // ================= Versions =================

    public ObservableCollection<GameVersionItem> VisibleVersions { get; } = new();

    private GameVersionItem? _selectedVersion;
    public GameVersionItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (Set(ref _selectedVersion, value))
            {
                RaiseCanPlay();
                if (value != null) SettingsService.Instance.LastVersion = value.Name;
            }
        }
    }

    private bool _showSnapshots;
    public bool ShowSnapshots
    {
        get => _showSnapshots;
        set { if (Set(ref _showSnapshots, value)) { SettingsService.Instance.ShowSnapshots = value; ApplyVersionFilter(); } }
    }

    private bool _showOldVersions;
    public bool ShowOldVersions
    {
        get => _showOldVersions;
        set { if (Set(ref _showOldVersions, value)) { SettingsService.Instance.ShowOldVersions = value; ApplyVersionFilter(); } }
    }

    private string _versionFilterText = "";
    public string VersionFilterText
    {
        get => _versionFilterText;
        set { if (Set(ref _versionFilterText, value)) ApplyVersionFilter(); }
    }

    // ================= Accounts =================

    public ObservableCollection<OfflineAccount> Accounts { get; }

    private OfflineAccount? _selectedAccount;
    public OfflineAccount? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (Set(ref _selectedAccount, value))
            {
                AccountService.Instance.Select(value);
                RaiseCanPlay();
            }
        }
    }

    private string _newAccountName = "";
    public string NewAccountName
    {
        get => _newAccountName;
        set => Set(ref _newAccountName, value);
    }

    // ================= Settings =================

    private string _gameDirectory = "";
    public string GameDirectory
    {
        get => _gameDirectory;
        set => Set(ref _gameDirectory, value);
    }

    private int _maxRamMb = 2048;
    public int MaxRamMb
    {
        get => _maxRamMb;
        set { Set(ref _maxRamMb, value); OnPropertyChanged(nameof(MaxRamGbLabel)); }
    }

    public string MaxRamGbLabel => $"{MaxRamMb / 1024.0:0.0} GB";

    private ObservableCollection<JavaInstall> _javaInstalls = new();
    public ObservableCollection<JavaInstall> JavaInstalls
    {
        get => _javaInstalls;
        set => Set(ref _javaInstalls, value);
    }

    private JavaInstall? _selectedJava;
    public JavaInstall? SelectedJava
    {
        get => _selectedJava;
        set => Set(ref _selectedJava, value);
    }

    private bool _closeLauncherOnStart = true;
    public bool CloseLauncherOnStart
    {
        get => _closeLauncherOnStart;
        set => Set(ref _closeLauncherOnStart, value);
    }

    // ================= Console =================

    public ObservableCollection<string> ConsoleLines { get; } = new();

    // ================= Commands =================

    public RelayCommand PlayCommand { get; }
    public RelayCommand RefreshVersionsCommand { get; }
    public RelayCommand AddAccountCommand { get; }
    public RelayCommand RemoveAccountCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand OpenGameDirCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }

    // ================= Initialization =================

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            StatusText = "Scanning Java installations…";
            await JavaService.Instance.ScanAsync();
            RefreshJavaList();

            StatusText = "Loading version list…";
            await RefreshVersionsAsync(silent: true);

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Init failed: {ex}");
            StatusText = $"Error: {ex.Message}";
            AppendConsole($"[ERROR] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshVersionsAsync(bool silent = false)
    {
        IsBusy = true;
        try
        {
            var launcher = GameLauncherService.Instance.GetLauncher();
            await VersionService.Instance.LoadAsync(launcher, _cts.Token);
            ApplyVersionFilter(restoreSelection: true);
            if (!silent) StatusText = "Version list updated";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogService.Instance.Error($"Refresh versions failed: {ex}");
            StatusText = $"Could not load versions: {ex.Message}";
            AppendConsole($"[ERROR] {ex.Message}");
            MessageBox.Show(
                "Failed to load the version list from Mojang.\n\n" + ex.Message +
                "\n\nCheck your internet connection and try again.",
                AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyVersionFilter(bool restoreSelection = false)
    {
        var prev = SelectedVersion?.Name;
        var filtered = VersionService.Instance.Filter(ShowSnapshots, ShowOldVersions);
        if (!string.IsNullOrWhiteSpace(VersionFilterText))
            filtered = filtered.Where(v =>
                v.Name.Contains(VersionFilterText, StringComparison.OrdinalIgnoreCase));

        VisibleVersions.Clear();
        foreach (var v in filtered) VisibleVersions.Add(v);

        if (restoreSelection)
        {
            var want = prev ?? SettingsService.Instance.LastVersion ?? VersionService.Instance.LatestRelease;
            SelectedVersion = VisibleVersions.FirstOrDefault(v => v.Name == want)
                              ?? VisibleVersions.FirstOrDefault(v => v.IsRelease)
                              ?? VisibleVersions.FirstOrDefault();
        }
    }

    private void OnNewVersionDetected(string name)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            AppendConsole($"[NEW] Mojang released a new version: {name}");
            StatusText = $"New version available: {name}";
        });
    }

    // ================= Play =================

    private async Task PlayAsync()
    {
        var version = SelectedVersion;
        var account = SelectedAccount;
        if (version == null || account == null) return;

        // Persist pending settings edits
        SaveSettings();

        IsBusy = true;
        Progress = 0;
        try
        {
            var process = await GameLauncherService.Instance.InstallAndLaunchAsync(
                version.Name, account, _cts.Token);

            process.OutputDataReceived += (_, e) => { if (e.Data != null) AppendConsole(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) AppendConsole(e.Data); };
            try
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch { /* redirect not available if UseShellExecute — fine */ }

            process.Exited += (_, _) => App.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Game exited (code {process.ExitCode})";
                AppendConsole($"[GAME] exited with code {process.ExitCode}");
            });

            Progress = 1;
            if (CloseLauncherOnStart)
            {
                App.Current.Dispatcher.Invoke(() => App.Current.Shutdown());
                return;
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Launch failed: {ex}");
            StatusText = $"Launch failed: {ex.Message}";
            AppendConsole($"[ERROR] Launch failed: {ex.Message}");
            MessageBox.Show(
                $"Failed to launch {version.Name}.\n\n{ex.Message}",
                AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            _ = Task.Delay(1200).ContinueWith(_ =>
                App.Current.Dispatcher.Invoke(() => Progress = 0));
        }
    }

    private void OnLaunchProgress(LaunchProgress p)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            // Combine file + byte progress for a smoother bar
            var combined = p.FileRatio * 0.5 + p.ByteRatio * 0.5;
            Progress = Math.Clamp(combined, 0, 0.99);
            if (!string.IsNullOrEmpty(p.CurrentFile))
                StatusText = $"Installing ({p.FilesDone}/{p.FilesTotal}) {p.CurrentFile}";
        });
    }

    // ================= Accounts =================

    private void AddAccount()
    {
        var name = NewAccountName.Trim();
        if (name.Length == 0) return;

        if (!OfflineAccount.IsValidUsername(name))
        {
            MessageBox.Show(
                "Invalid username.\n\nRules: 2–16 characters, letters, digits and underscore only.",
                AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AccountService.Instance.Add(name);
        SelectedAccount = AccountService.Instance.SelectedAccount;
        NewAccountName = "";
        AppendConsole($"[ACCOUNT] added offline account '{name}'");
    }

    private void RemoveAccount()
    {
        var acc = SelectedAccount;
        if (acc == null) return;
        if (Accounts.Count <= 1)
        {
            MessageBox.Show("Keep at least one account for testing.",
                AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        AccountService.Instance.Remove(acc);
        SelectedAccount = AccountService.Instance.SelectedAccount;
    }

    // ================= Settings =================

    private void LoadSettingsIntoUi()
    {
        var s = SettingsService.Instance;
        GameDirectory = s.GameDirectory;
        MaxRamMb = s.MaxRamMb;
        ShowSnapshots = s.ShowSnapshots;
        ShowOldVersions = s.ShowOldVersions;
        CloseLauncherOnStart = s.CloseLauncherOnStart;
    }

    private void RefreshJavaList()
    {
        JavaInstalls.Clear();
        foreach (var j in JavaService.Instance.Installs) JavaInstalls.Add(j);

        var wanted = SettingsService.Instance.JavaPath;
        SelectedJava = wanted != null
            ? JavaInstalls.FirstOrDefault(j => j.Path.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            : JavaInstalls.FirstOrDefault();
    }

    private void SaveSettings()
    {
        var s = SettingsService.Instance;
        if (!string.IsNullOrWhiteSpace(GameDirectory) &&
            !string.Equals(GameDirectory, s.GameDirectory, StringComparison.OrdinalIgnoreCase))
        {
            s.GameDirectory = GameDirectory.TrimEnd('\\', '/');
        }
        s.MaxRamMb = MaxRamMb;
        s.CloseLauncherOnStart = CloseLauncherOnStart;
        s.JavaPath = SelectedJava?.Path;
        s.Save();
        StatusText = "Settings saved";
        AppendConsole("[SETTINGS] saved");
    }

    private void ResetSettings()
    {
        var result = MessageBox.Show(
            "Reset all settings to defaults?\n\nGame directory will point back to %APPDATA%\\ManiaLauncher\\game. Installed game files are not deleted.",
            AppInfo.Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var s = SettingsService.Instance;
        s.GameDirectory = SettingsService.DefaultGameDirectory();
        s.MaxRamMb = 2048;
        s.JavaPath = null;
        s.ShowSnapshots = false;
        s.ShowOldVersions = false;
        s.CloseLauncherOnStart = true;
        s.Save();
        LoadSettingsIntoUi();
        RefreshJavaList();
        StatusText = "Settings reset to defaults";
        AppendConsole("[SETTINGS] reset to defaults");
    }

    public void BrowseGameDirectory()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose game directory",
            InitialDirectory = Directory.Exists(GameDirectory) ? GameDirectory : null
        };
        if (dlg.ShowDialog() == true)
        {
            GameDirectory = dlg.FolderName;
            SaveSettings();
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn($"Open folder failed: {ex.Message}");
        }
    }

    // ================= Console =================

    private void AppendConsole(string line)
    {
        if (App.Current == null) return;
        App.Current.Dispatcher.Invoke(() =>
        {
            ConsoleLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            while (ConsoleLines.Count > 2000) ConsoleLines.RemoveAt(0);
        });
    }

    public void Shutdown()
    {
        try { _cts.Cancel(); } catch { }
    }
}
