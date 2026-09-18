using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace ManiaLauncher.Installer;

public partial class MainWindow : Window
{
    private string _installPath;

    public MainWindow()
    {
        InitializeComponent();
        _installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManiaLauncher");
        PathBox.Text = _installPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку для установки Mania Launcher",
            UseDescriptionForTitle = true,
            SelectedPath = _installPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
            !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _installPath = dialog.SelectedPath;
            PathBox.Text = _installPath;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        InstallBtn.IsEnabled = false;
        StatusText.Text = "Подготовка к установке...";
        Progress.Value = 0;

        bool createDesktop = DesktopShortcut.IsChecked == true;
        bool createStartMenu = StartMenuShortcut.IsChecked == true;
        string installPath = _installPath;

        try
        {
            // Ищем файлы лаунчера ДО фонового потока
            string? sourceAppDir = FindLauncherFiles();

            if (sourceAppDir == null)
            {
                MessageBox.Show(
                    "Не найдены файлы лаунчера!\n\n" +
                    "Рядом с установщиком должна быть папка App\n" +
                    "с файлом ManiaLauncher.exe.\n\n" +
                    "Сделайте так:\n" +
                    "1. dotnet publish ... -o publish/App\n" +
                    "2. Copy-Item publish\\App → publish\\Installer\\App\n" +
                    "3. Запустите установщик из publish\\Installer",
                    "Файлы не найдены",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InstallBtn.IsEnabled = true;
                StatusText.Text = "Файлы лаунчера не найдены";
                return;
            }

            await Task.Run(() =>
            {
                UpdateStatus("Создание папок...", 10);

                Directory.CreateDirectory(installPath);
                Directory.CreateDirectory(Path.Combine(installPath, "data"));
                Directory.CreateDirectory(Path.Combine(installPath, "logs"));

                UpdateStatus("Копирование файлов лаунчера...", 30);

                // Копируем всё содержимое App в папку установки
                CopyDirectory(sourceAppDir, installPath);

                var exePath = Path.Combine(installPath, "ManiaLauncher.exe");
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException(
                        "После копирования не найден ManiaLauncher.exe.\n" +
                        "Проверьте папку App рядом с установщиком.");
                }

                UpdateStatus("Создание ярлыков...", 80);

                if (createDesktop)
                {
                    CreateShortcut(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Mania Launcher.lnk"),
                        exePath,
                        "Mania Launcher — от Mania AI");
                }

                if (createStartMenu)
                {
                    var startMenu = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs", "Mania Launcher");
                    Directory.CreateDirectory(startMenu);
                    CreateShortcut(
                        Path.Combine(startMenu, "Mania Launcher.lnk"),
                        exePath,
                        "Mania Launcher — от Mania AI");
                }

                UpdateStatus("Готово!", 100);
            });

            var finalExe = Path.Combine(installPath, "ManiaLauncher.exe");

            StatusText.Text = "Установка завершена!";
            MessageBox.Show(
                $"Mania Launcher успешно установлен в:\n{installPath}\n\n" +
                "Ярлыки созданы. Можно запускать!",
                "Установка завершена",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (File.Exists(finalExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = finalExe,
                    UseShellExecute = true,
                    WorkingDirectory = installPath
                });
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка установки:\n\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusText.Text = "Ошибка установки";
            InstallBtn.IsEnabled = true;
            Progress.Value = 0;
        }
    }

    /// <summary>
    /// Ищет папку с ManiaLauncher.exe рядом с установщиком.
    /// </summary>
    private static string? FindLauncherFiles()
    {
        var installerDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Возможные места, где лежит App
        var candidates = new[]
        {
            Path.Combine(installerDir, "App"),
            Path.Combine(installerDir, "ManiaLauncher"),
            installerDir, // иногда файлы лежат прямо рядом
            Path.Combine(Directory.GetParent(installerDir)?.FullName ?? "", "App"),
            Path.Combine(Directory.GetParent(installerDir)?.FullName ?? "", "ManiaLauncher"),
        };

        foreach (var dir in candidates)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var exe = Path.Combine(dir, "ManiaLauncher.exe");
            if (File.Exists(exe))
                return dir;
        }

        return null;
    }

    private void UpdateStatus(string text, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            Progress.Value = progress;
        });
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            // Не копируем сам установщик, если он вдруг лежит в той же папке
            if (fileName.Equals("ManiaLauncher.Installer.exe", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("ManiaLauncher.Installer.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            var dest = Path.Combine(destDir, fileName);
            File.Copy(file, dest, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (name.Equals("App", StringComparison.OrdinalIgnoreCase))
                continue; // избегаем рекурсии

            var dest = Path.Combine(destDir, name);
            CopyDirectory(dir, dest);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        try
        {
            // Экранируем пути для PowerShell
            string Escape(string s) => s.Replace("'", "''");

            var ps = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{Escape(shortcutPath)}')
$Shortcut.TargetPath = '{Escape(targetPath)}'
$Shortcut.WorkingDirectory = '{Escape(Path.GetDirectoryName(targetPath) ?? "")}'
$Shortcut.Description = '{Escape(description)}'
$Shortcut.Save()
";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(8000);
        }
        catch
        {
            // ярлык — по возможности
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
