using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;

namespace Universe3D.Tests;

public class VelocityArrowTests
{
    [Fact]
    public void Arrow_M31_PointsTowardSun()
    {
        var position = SkyMath.UnitVector(10.68, 41.27) * 0.77;
        var arrow = VelocityArrow.Compute(position, czHelio: -300, z: -0.001, pecKms: null, ArrowMode.Observed);
        Assert.NotNull(arrow);
        Assert.True(arrow!.Approaching);
        Assert.True(arrow.Direction.Dot(-position.Normalized()) > 0.9999);
        Assert.InRange(arrow.LengthPx, 18 * Math.Log10(4) - 0.01, 18 * Math.Log10(4) + 0.01);
        Assert.InRange(arrow.LengthPx, 10.83, 10.85);
    }

    [Fact]
    public void Arrow_Coma_PointsAway()
    {
        var position = SkyMath.UnitVector(194.95, 27.98) * 100;
        var arrow = VelocityArrow.Compute(position, czHelio: 6925, z: 0.0231, pecKms: null, ArrowMode.Observed);
        Assert.NotNull(arrow);
        Assert.False(arrow!.Approaching);
        Assert.True(arrow.Direction.Dot(position.Normalized()) > 0.9999);
        Assert.InRange(arrow.LengthPx, 33.24 - 0.01, 33.24 + 0.01);
    }

    [Fact]
    public void Arrow_HighZQuasar_Clamped()
    {
        var position = SkyMath.UnitVector(150, 2) * 8800;
        var arrow = VelocityArrow.Compute(position, czHelio: null, z: 7, pecKms: null, ArrowMode.Observed);
        Assert.NotNull(arrow);
        Assert.False(arrow!.Approaching);
        Assert.Equal(60, arrow.LengthPx);
    }

    [Fact]
    public void Arrow_PeculiarWithoutMeasured_IsNull()
    {
        var position = SkyMath.UnitVector(150, 2) * 30;
        Assert.Null(VelocityArrow.Compute(position, czHelio: 2100, z: 0.007, pecKms: null, ArrowMode.PeculiarVelocity));

        var withPec = VelocityArrow.Compute(position, czHelio: 2100, z: 0.007, pecKms: -250, ArrowMode.PeculiarVelocity);
        Assert.NotNull(withPec);
        Assert.True(withPec!.Approaching);
        Assert.InRange(withPec.LengthPx, 18 * Math.Log10(3.5) - 1e-9, 18 * Math.Log10(3.5) + 1e-9);
    }

    [Fact]
    public void Arrow_SlowObject_ClampedToMinimum()
    {
        var position = SkyMath.UnitVector(150, 2) * 3;
        var arrow = VelocityArrow.Compute(position, czHelio: 10, z: 0.00003, pecKms: null, ArrowMode.Observed);
        Assert.Equal(3, arrow!.LengthPx);
    }
}
