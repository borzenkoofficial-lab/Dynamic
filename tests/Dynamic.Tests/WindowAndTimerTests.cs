using Xunit;
using Dynamic.Core;
using Dynamic.Services;

namespace Dynamic.Tests;

public sealed class WindowGeometryMathTests
{
    [Fact]
    public void Calculate_UsesMonitorOriginAndDpiScale()
    {
        var result = WindowGeometryMath.Calculate(-1920, 40, 2560, 144, expanded: true);
        Assert.Equal(-1030, result.X);
        Assert.Equal(52, result.Y);
        Assert.Equal(780, result.Width);
        Assert.Equal(930, result.Height);
        Assert.Equal(45, result.Radius);
    }

    [Fact]
    public void SpringProgress_HasStableEndpoints()
    {
        Assert.Equal(0, WindowGeometryMath.SpringProgress(0, 0.38), 6);
        Assert.Equal(1, WindowGeometryMath.SpringProgress(0.38, 0.38), 6);
        Assert.Equal(1, WindowGeometryMath.SpringProgress(2, 0.38), 6);
    }

    [Fact]
    public void Interpolate_UsesCurrentFrameAsNewStartWhenInterrupted()
    {
        var compact = new WindowGeometryFrame(100, 10, 390, 76, 38);
        var expanded = new WindowGeometryFrame(35, 10, 520, 620, 30);
        var halfway = WindowGeometryMath.Interpolate(compact, expanded, 0.5);
        var reversed = WindowGeometryMath.Interpolate(halfway, compact, 0);
        Assert.Equal(halfway, reversed);
        Assert.Equal(compact, WindowGeometryMath.Interpolate(halfway, compact, 1));
    }
}

public sealed class CountdownTimerServiceTests
{
    [Fact]
    public void Restore_ContinuesRunningDeadlineAcrossServiceInstances()
    {
        var first = new CountdownTimerService();
        first.Reset(TimeSpan.FromMinutes(2));
        first.Start();
        var snapshot = first.ExportState();

        var second = new CountdownTimerService();
        second.Restore(snapshot);

        Assert.True(second.IsRunning);
        Assert.True(second.Remaining > TimeSpan.Zero);
        Assert.True(second.Remaining <= TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Restore_ExpiredDeadlineStopsAtZero()
    {
        var service = new CountdownTimerService();
        service.Restore(new CountdownTimerSnapshot(
            TimeSpan.FromMinutes(1).Ticks,
            TimeSpan.FromMinutes(1).Ticks,
            DateTimeOffset.UtcNow.AddSeconds(-1)));

        Assert.False(service.IsRunning);
        Assert.Equal(TimeSpan.Zero, service.Remaining);
        Assert.True(service.Update());
        Assert.False(service.Update());
    }
}
