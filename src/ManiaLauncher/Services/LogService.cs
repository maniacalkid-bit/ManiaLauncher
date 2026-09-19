using System.IO;

namespace ManiaLauncher.Services;

/// <summary>
/// Minimal rolling file logger. Writes to %APPDATA%\ManiaLauncher\logs\launcher.log
/// and keeps the last N log files.
/// </summary>
public sealed class LogService
{
    private static readonly Lazy<LogService> Lazy = new(() => new LogService());
    public static LogService Instance => Lazy.Value;

    private readonly object _gate = new();
    private readonly string _file;
    private readonly int _maxFiles = 5;
    private readonly long _maxBytes = 2 * 1024 * 1024;

    private LogService()
    {
        Directory.CreateDirectory(AppInfo.LogsDir);
        RollOldLogs();
        _file = Path.Combine(AppInfo.LogsDir, "launcher.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);
    public void Fatal(string message) => Write("FATAL", message);

    public void Write(string level, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            lock (_gate)
            {
                TruncateIfNeeded();
                File.AppendAllText(_file, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    private void TruncateIfNeeded()
    {
        var fi = new FileInfo(_file);
        if (!fi.Exists || fi.Length <= _maxBytes) return;

        var rolled = Path.Combine(
            AppInfo.LogsDir,
            $"launcher-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        try
        {
            File.Move(_file, rolled, true);
            RollOldLogs();
        }
        catch { /* ignore */ }
    }

    private void RollOldLogs()
    {
        try
        {
            var dir = new DirectoryInfo(AppInfo.LogsDir);
            var files = dir.GetFiles("launcher*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(_maxFiles);
            foreach (var old in files) old.Delete();
        }
        catch { /* ignore */ }
    }
}
