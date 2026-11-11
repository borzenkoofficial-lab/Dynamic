using System.Text.Json;

namespace Dynamic.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dynamic");
    private string FilePath => Path.Combine(_directory, "settings.json");

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        if (!File.Exists(FilePath))
            return new UserSettings();

        try
        {
            await using var stream = File.OpenRead(FilePath);
            var settings = await JsonSerializer.DeserializeAsync<UserSettings>(
                stream, JsonOptions, cancellationToken);
            return settings is { SchemaVersion: 1 } ? settings : new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            var backup = Path.Combine(_directory, $"settings.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            try { File.Copy(FilePath, backup, overwrite: false); } catch { /* best-effort recovery */ }
            return new UserSettings();
        }
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(_directory);
        var temporaryPath = FilePath + ".tmp";

        await using (var stream = new FileStream(
            temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}