using System.Diagnostics;
using System.Globalization;
using System.Net.Http;

namespace Dynamic.Services;

public sealed record TransferProgressSnapshot(
    string Title, double Percent, string Detail, string Speed, long BytesTransferred, long? TotalBytes);

public sealed class FileTransferService : IDisposable
{
    private readonly HttpClient _http = new();

    public async Task DownloadAsync(
        Uri source, string destinationPath, IProgress<TransferProgressSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsAbsoluteUri || (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Only HTTP and HTTPS download sources are supported.", nameof(source));

        using var response = await _http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await CopyWithProgressAsync(input, output, Path.GetFileName(destinationPath), total, progress, cancellationToken);
    }

    public async Task CopyFileAsync(
        string sourcePath, string destinationPath, IProgress<TransferProgressSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var total = input.Length;
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await CopyWithProgressAsync(input, output, Path.GetFileName(sourcePath), total, progress, cancellationToken);
    }

    private static async Task CopyWithProgressAsync(
        Stream input, Stream output, string title, long? total,
        IProgress<TransferProgressSnapshot>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long transferred = 0;
        var clock = Stopwatch.StartNew();
        long lastReport = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;

            if (clock.ElapsedMilliseconds - lastReport < 120 && (total is null || transferred < total)) continue;
            lastReport = clock.ElapsedMilliseconds;
            var seconds = Math.Max(0.001, clock.Elapsed.TotalSeconds);
            var bytesPerSecond = transferred / seconds;
            var percent = total is > 0 ? Math.Clamp(transferred * 100.0 / total.Value, 0, 100) : 0;
            var detail = total is > 0
                ? $"{FormatBytes(transferred)} / {FormatBytes(total.Value)}"
                : $"{FormatBytes(transferred)} передано";
            progress?.Report(new TransferProgressSnapshot(title, percent, detail,
                $"{FormatBytes((long)bytesPerSecond)}/с", transferred, total));
        }
        await output.FlushAsync(cancellationToken);
        var finalDetail = total is > 0 ? $"{FormatBytes(transferred)} / {FormatBytes(total.Value)}" : $"{FormatBytes(transferred)} передано";
        progress?.Report(new TransferProgressSnapshot(title, 100, finalDetail, "Готово", transferred, total));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value.ToString("0.0", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    public void Dispose() => _http.Dispose();
}
