using System.Text.Json;

namespace Dynamic.Services;

public sealed record CountdownTimerSnapshot(long DurationTicks, long RemainingTicks, DateTimeOffset? DeadlineUtc);

public sealed record DynamicPreferences(double GlassOpacity, CountdownTimerSnapshot? Timer);

public sealed class DynamicSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dynamic", "settings.json");

    public DynamicPreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new DynamicPreferences(0.64, null);
            return JsonSerializer.Deserialize<DynamicPreferences>(File.ReadAllText(_filePath))
                   ?? new DynamicPreferences(0.64, null);
        }
        catch (Exception)
        {
            return new DynamicPreferences(0.64, null);
        }
    }

    public void Save(DynamicPreferences preferences)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, JsonOptions));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
