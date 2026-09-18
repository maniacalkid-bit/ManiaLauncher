namespace ManiaLauncher.Core.Utils;

public static class Paths
{
    public static string AppData => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ManiaLauncher");

    public static string GameDirectory => Path.Combine(AppData, "minecraft");
    public static string DataDirectory => Path.Combine(AppData, "data");
    public static string LogsDirectory => Path.Combine(AppData, "logs");
    public static string JavaDirectory => Path.Combine(AppData, "java");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppData);
        Directory.CreateDirectory(GameDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(JavaDirectory);
    }
}
