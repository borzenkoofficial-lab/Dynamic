using System.Reflection;
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
                    .AppendLine($"Runtime: {Environment.Version}");
                AppendException(entry, exception, 0);
                entry.AppendLine(new string('-', 72));
                File.AppendAllText(LogPath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never cause a second startup failure.
        }

        return LogPath;
    }

    private static void AppendException(StringBuilder output, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        output.AppendLine($"{indent}{exception.GetType().FullName}: {exception.Message}");
        output.AppendLine($"{indent}HResult: 0x{exception.HResult:X8}");
        output.AppendLine($"{indent}Details: {exception}");

        // XamlParseException exposes source coordinates on some Windows App SDK versions.
        // Read them reflectively so diagnostics remain compatible across SDK versions.
        AppendProperty(output, exception, "LineNumber", indent);
        AppendProperty(output, exception, "LinePosition", indent);
        AppendProperty(output, exception, "BaseUri", indent);

        if (exception.InnerException is not null)
        {
            output.AppendLine($"{indent}Inner exception:");
            AppendException(output, exception.InnerException, depth + 1);
        }
    }

    private static void AppendProperty(StringBuilder output, Exception exception, string name, string indent)
    {
        try
        {
            var property = exception.GetType().GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var value = property?.GetValue(exception);
            if (value is not null)
                output.AppendLine($"{indent}{name}: {value}");
        }
        catch
        {
            // Optional diagnostic metadata is not guaranteed by every XAML runtime.
        }
    }
}
