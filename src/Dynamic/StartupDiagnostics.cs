using System.Text;

namespace Dynamic;

internal static class StartupDiagnostics
{
    private static readonly object Sync = new();

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dynamic", "logs", "startup.log");

    public static string Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var entry = new StringBuilder()
                    .AppendLine($"[{DateTimeOffset.Now:O}] Unhandled application error")
                    .AppendLine($"OS: {Environment.OSVersion}")
                    .AppendLine($"Runtime: {Environment.Version}")
                    .AppendLine(exception.ToString())
                    .AppendLine(new string('-', 72))
                    .ToString();
                File.AppendAllText(LogPath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never cause a second startup failure.
        }

        return LogPath;
    }
}