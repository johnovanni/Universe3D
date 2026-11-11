using Universe3D.Core.Display;

namespace Universe3D.Tests;

public class GpuCalibrationTests
{
    private const string Unknown = "Some Unknown GPU";

    [Fact]
    public void Calibration_ClassifiesByMedianAfterSkippedFrames()
    {
        var c = new GpuCalibration(Unknown);
        GpuClass? result = null;
        for (int i = 0; i < GpuCalibration.SkipFrames; i++) Assert.Null(c.Step(TimeSpan.FromSeconds(1), true, 500));
        for (int i = 0; i < GpuCalibration.Frames; i++) result = c.Step(TimeSpan.FromSeconds(2), true, 4);
        Assert.Equal(GpuClass.Recommended, result);
        Assert.False(c.IsRunning);
        Assert.False(c.GaveUp);
    }

    [Fact]
    public void Calibration_WaitsForTheFirstDataSet()
    {
        var c = new GpuCalibration(Unknown);
        for (int i = 0; i < 500; i++) Assert.Null(c.Step(TimeSpan.FromSeconds(1), false, 1));
        Assert.True(c.IsRunning);
    }

    /// <summary>If the first data set never uploads, calibration stops instead of keeping the view animating forever.</summary>
    [Fact]
    public void Calibration_GivesUpWhenNoDataSetIsShownInTime()
    {
        var c = new GpuCalibration(Unknown);
        Assert.Null(c.Step(GpuCalibration.MaxWait - TimeSpan.FromMilliseconds(1), false, 1));
        Assert.True(c.IsRunning);
        Assert.Null(c.Step(GpuCalibration.MaxWait, false, 1));
        Assert.False(c.IsRunning);
        Assert.True(c.GaveUp);
        Assert.Null(c.Step(GpuCalibration.MaxWait + TimeSpan.FromSeconds(1), true, 1)); // stays stopped
    }
}
