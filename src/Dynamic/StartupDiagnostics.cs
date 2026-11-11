using System.Reflection;
using System.Runtime.InteropServices;
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
                AppendRestrictedErrorInfo(entry);
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

    private static void AppendRestrictedErrorInfo(StringBuilder output)
    {
        try
        {
            if (GetRestrictedErrorInfo(out var info) < 0 || info is null)
                return;

            if (info.GetErrorDetails(
                    out var description,
                    out var error,
                    out var restrictedDescription,
                    out var capabilitySid) >= 0)
            {
                output.AppendLine("WinRT restricted error information:");
                output.AppendLine($"  Error: 0x{error:X8}");
                if (!string.IsNullOrWhiteSpace(description))
                    output.AppendLine($"  Description: {description}");
                if (!string.IsNullOrWhiteSpace(restrictedDescription))
                    output.AppendLine($"  Restricted description: {restrictedDescription}");
                if (!string.IsNullOrWhiteSpace(capabilitySid))
                    output.AppendLine($"  Capability SID: {capabilitySid}");
            }

            if (info.GetReference(out var reference) >= 0 && !string.IsNullOrWhiteSpace(reference))
                output.AppendLine($"  Reference: {reference}");
        }
        catch (Exception diagnosticException)
        {
            output.AppendLine($"WinRT error details unavailable: {diagnosticException.GetType().Name}: {diagnosticException.Message}");
        }
    }

    private static void AppendException(StringBuilder output, Exception exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        output.AppendLine($"{indent}{exception.GetType().FullName}: {exception.Message}");
        output.AppendLine($"{indent}HResult: 0x{exception.HResult:X8}");
        output.AppendLine($"{indent}Details: {exception}");

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

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int GetRestrictedErrorInfo(out IRestrictedErrorInfo? errorInfo);

    [ComImport]
    [Guid("82BA7092-4C88-427D-A7BC-16DD93FEB67E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRestrictedErrorInfo
    {
        [PreserveSig]
        int GetErrorDetails(
            [MarshalAs(UnmanagedType.BStr)] out string description,
            out int error,
            [MarshalAs(UnmanagedType.BStr)] out string restrictedDescription,
            [MarshalAs(UnmanagedType.BStr)] out string capabilitySid);

        [PreserveSig]
        int GetReference([MarshalAs(UnmanagedType.BStr)] out string reference);
    }
}
