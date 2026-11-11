using Universe3D.Core.Display;

namespace Universe3D.Tests;

public class ScaleRingsTests
{
    [Fact]
    public void ScaleRings_Decades()
    {
        var radii = ScaleRings.RadiiLy(1e6);
        Assert.Contains(1e5, radii);
        Assert.Contains(1e6, radii);
        Assert.All(radii, r => Assert.InRange(r, 5e4, 2e6));
        Assert.Equal(new[] { 1e5, 3e5, 1e6 }, radii);
    }

    [Theory]
    [InlineData(3e9, new[] { 3e8, 1e9, 3e9 })]
    [InlineData(2e4, new[] { 1e3, 3e3, 1e4, 3e4 })]
    [InlineData(500, new[] { 30.0, 100, 300, 1000 })]
    public void ScaleRings_OneThreeSpacing_BetweenDOver20And2D(double d, double[] expected)
    {
        var radii = ScaleRings.RadiiLy(d);
        Assert.Equal(expected.Length, radii.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], radii[i], expected[i] * 1e-12);
    }

    [Fact]
    public void ScaleRings_BoundsAreInclusive()
    {
        // d = 2e6: d/20 = 1e5 and 2d = 4e6 -> 1e5 is kept, 3e6 is kept.
        Assert.Equal(new[] { 1e5, 3e5, 1e6, 3e6 }, ScaleRings.RadiiLy(2e6));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ScaleRings_InvalidDistance_Empty(double d) => Assert.Empty(ScaleRings.RadiiLy(d));

    [Theory]
    [InlineData(1e6, "1 Mly")]
    [InlineData(3e5, "300 kly")]
    [InlineData(1e5, "100 kly")]
    [InlineData(3e9, "3 Gly")]
    [InlineData(1e10, "10 Gly")]
    [InlineData(30, "30 ly")]
    [InlineData(1e3, "1 kly")]
    public void ScaleRings_Label(double ly, string expected) => Assert.Equal(expected, ScaleRings.Label(ly));
}
