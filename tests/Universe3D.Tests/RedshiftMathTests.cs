using Universe3D.Core.Cosmology;

namespace Universe3D.Tests;

public class RedshiftMathTests
{
    [Fact]
    public void HelioToCmb_AddsDipoleAlongApex()
    {
        Assert.Equal(1000 + 369.8, RedshiftMath.HelioToCmb(1000, 264.0, 48.3), 2);
        // Opposite direction: l + 180, b negated.
        Assert.Equal(1000 - 369.8, RedshiftMath.HelioToCmb(1000, 84.0, -48.3), 2);
    }

    [Fact]
    public void HelioToLocalGroup_AddsSinLCosB()
    {
        Assert.Equal(300, RedshiftMath.HelioToLocalGroup(0, 90, 0), 9);
        Assert.Equal(0, RedshiftMath.HelioToLocalGroup(0, 0, 0), 9);
    }

    [Theory]
    [InlineData(-300, -0.001, "Blueshifted: approaching at 300 km/s")]
    [InlineData(1284, 0.00428, "Redshifted: receding at 1,284 km/s")]
    [InlineData(47370, 0.158, "Redshifted: recession due to cosmic expansion (z = 0.158)")]
    public void ShiftDescription_Texts(double cz, double z, string expected)
    {
        Assert.Equal(expected, RedshiftMath.ShiftDescription(cz, z));
    }
}
