namespace Dynamic.Core;

public readonly record struct WindowGeometryFrame(int X, int Y, int Width, int Height, int Radius);

public static class WindowGeometryMath
{
    public static WindowGeometryFrame Calculate(
        int workX, int workY, int workWidth, uint dpi, bool expanded)
    {
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        var width = (int)Math.Round((expanded ? 520 : 390) * scale);
        var height = (int)Math.Round((expanded ? 620 : 76) * scale);
        return new WindowGeometryFrame(
            workX + (workWidth - width) / 2,
            workY + (int)Math.Round(8 * scale),
            width,
            height,
            (int)Math.Round((expanded ? 30 : 38) * scale));
    }

    public static double SpringProgress(double elapsedSeconds, double durationSeconds)
    {
        if (durationSeconds <= 0) return 1;
        var t = Math.Clamp(elapsedSeconds / durationSeconds, 0, 1);
        if (t >= 1) return 1;
        return 1 - Math.Exp(-7.5 * t) * (Math.Cos(8 * t) + 0.9 * Math.Sin(8 * t));
    }

    public static WindowGeometryFrame Interpolate(
        WindowGeometryFrame from, WindowGeometryFrame to, double progress) =>
        new(
            Lerp(from.X, to.X, progress),
            Lerp(from.Y, to.Y, progress),
            Lerp(from.Width, to.Width, progress),
            Lerp(from.Height, to.Height, progress),
            Lerp(from.Radius, to.Radius, progress));

    private static int Lerp(int from, int to, double progress) =>
        (int)Math.Round(from + (to - from) * progress);
}
