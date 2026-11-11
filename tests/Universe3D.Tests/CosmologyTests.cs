using Universe3D.Core.Cosmology;

namespace Universe3D.Tests;

public class CosmologyTests
{
    [Theory]
    [InlineData(0.1, 418.5)]
    [InlineData(1, 3303.8)]
    [InlineData(3, 6355.7)]
    [InlineData(7, 8627.6)]
    public void ComovingDistance_MatchesReference(double z, double expectedMpc)
    {
        double d = CosmologyModel.Default.ComovingDistanceMpc(z);
        Assert.InRange(d, expectedMpc * 0.999, expectedMpc * 1.001);
    }

    [Theory]
    [InlineData(0.1, 1.301)]
    [InlineData(1, 7.715)]
    [InlineData(3, 11.354)]
    [InlineData(7, 12.716)]
    public void Lookback_MatchesReference(double z, double expectedGyr)
    {
        double t = CosmologyModel.Default.LookbackTimeGyr(z);
        Assert.InRange(t, expectedGyr * 0.999, expectedGyr * 1.001);
    }

    [Fact]
    public void RedshiftAtDistance_InvertsComoving()
    {
        var m = CosmologyModel.Default;
        double d = m.ComovingDistanceMpc(0.5);
        Assert.Equal(0.5, m.RedshiftAtComovingDistance(d), 6);
    }

    [Fact]
    public void BeyondTable_IntegratesDirectlyAndInverts()
    {
        var m = CosmologyModel.Default;
        double d = m.ComovingDistanceMpc(10);
        Assert.True(d > m.ComovingDistanceMpc(8));
        Assert.Equal(10, m.RedshiftAtComovingDistance(d), 6);
    }

    [Fact]
    public void ZeroRedshift_IsZero()
    {
        Assert.Equal(0, CosmologyModel.Default.ComovingDistanceMpc(0));
        Assert.Equal(0, CosmologyModel.Default.LookbackTimeGyr(0));
    }
}
