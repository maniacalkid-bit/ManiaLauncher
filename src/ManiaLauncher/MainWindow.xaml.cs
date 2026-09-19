using ManiaLauncher.Services;
using ManiaLauncher.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ManiaLauncher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        // Auto-scroll console when new lines arrive
        _vm.ConsoleLines.CollectionChanged += (_, _) =>
        {
            if (ConsoleList.Items.Count > 0)
                ConsoleList.ScrollIntoView(ConsoleList.Items[^1]);
        };

        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Persist window size
        if (WindowState == WindowState.Normal)
        {
            SettingsService.Instance.WindowWidth = (int)Width;
            SettingsService.Instance.WindowHeight = (int)Height;
        }
        _vm.Shutdown();
    }

    // ---------- title bar ----------

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ---------- navigation ----------

    private void NavHome_Click(object sender, RoutedEventArgs e) => _vm.CurrentPage = NavPage.Home;
    private void NavVersions_Click(object sender, RoutedEventArgs e) => _vm.CurrentPage = NavPage.Versions;
    private void NavAccounts_Click(object sender, RoutedEventArgs e) => _vm.CurrentPage = NavPage.Accounts;
    private void NavSettings_Click(object sender, RoutedEventArgs e) => _vm.CurrentPage = NavPage.Settings;
    private void NavConsole_Click(object sender, RoutedEventArgs e) => _vm.CurrentPage = NavPage.Console;

    private void VersionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SelectedVersion != null) _vm.PlayCommand.Execute(null);
    }

    // ---------- accounts page ----------

    private void NewAccountBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.AddAccountCommand.CanExecute(null))
            _vm.AddAccountCommand.Execute(null);
    }

    private void SelectAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OfflineAccount acc })
            _vm.SelectedAccount = acc;
    }

    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OfflineAccount acc })
        {
            _vm.SelectedAccount = acc;
            if (_vm.RemoveAccountCommand.CanExecute(null))
                _vm.RemoveAccountCommand.Execute(null);
        }
    }

    // ---------- settings page ----------

    private void BrowseGameDir_Click(object sender, RoutedEventArgs e) => _vm.BrowseGameDirectory();
}
