namespace Dynamic.Settings;

public sealed class UserSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public bool AutoHide { get; set; }
    public bool ReduceMotion { get; set; }
    public bool PerformanceMode { get; set; }
    public string Theme { get; set; } = "system";
    public string AccentColor { get; set; } = "#B7F36B";
    public double Opacity { get; set; } = 0.96;
    public double CornerRadius { get; set; } = 28;
    public double AnimationSpeed { get; set; } = 1.0;
    public double SpringIntensity { get; set; } = 0.82;
}