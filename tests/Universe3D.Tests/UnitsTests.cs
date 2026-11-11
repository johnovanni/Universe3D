using Universe3D.Core.Text;

namespace Universe3D.Tests;

public class UnitsTests
{
    [Theory]
    [InlineData(0.77, "2.51 Mly (0.77 Mpc)")]
    [InlineData(0.0082, "26.7 kly (0.0082 Mpc)")]
    [InlineData(3303.8, "10.8 Gly (3,304 Mpc)")]
    public void Units_Format(double mpc, string expected) =>
        Assert.Equal(expected, Units.FormatDistance(mpc));

    [Theory]
    [InlineData(0.0000001, "0.326 ly")]
    [InlineData(0.306, "998 kly")]
    [InlineData(0.3066, "1.00 Mly")]   // 999.9 kly rounds up into the next unit
    [InlineData(1.0, "3.26 Mly")]
    [InlineData(306.6, "1.00 Gly")]
    public void Units_LightYearBoundaries(double mpc, string expected) =>
        Assert.Equal(expected, Units.FormatLightYears(mpc));

    [Theory]
    [InlineData(100.0, "100")]
    [InlineData(12.34, "12.3")]
    [InlineData(1.5, "1.5")]
    public void Units_MpcTrimsTrailingZeros(double mpc, string expectedMpc) =>
        Assert.EndsWith($"({expectedMpc} Mpc)", Units.FormatDistance(mpc));
}
