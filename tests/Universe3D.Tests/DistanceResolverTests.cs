using Universe3D.Core.Catalogs;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

public class DistanceResolverTests
{
    private static readonly CosmologyModel Cosmo = CosmologyModel.Default;
    private readonly DistanceResolver _r = new(Cosmo);

    // Equatorial position for a given galactic (l, b).
    private static (double ra, double dec) FromGalactic(double l, double b)
    {
        var eq = SkyFrames.EquatorialToGalactic.Transpose() * SkyMath.UnitVector(l, b);
        return SkyMath.ToSpherical(eq);
    }

    [Fact]
    public void Resolver_MeasuredWins()
    {
        var res = _r.Resolve(10.68, 41.27, 0.77, "UNGC", -300, null);
        Assert.NotNull(res);
        Assert.Equal(DistanceSource.Measured, res!.Source);
        Assert.Equal(0.77, res.DistanceMpc);
        Assert.Equal("UNGC", res.Catalog);
        Assert.NotNull(res.VCmb);
        Assert.NotNull(res.PeculiarKms);
        Assert.True(res.LookbackGyr >= 0 && !double.IsNaN(res.LookbackGyr));
    }

    [Fact]
    public void Resolver_MeasuredWithoutRedshift_UsesDistanceForLookback()
    {
        var res = _r.Resolve(10, 10, 100, "X", null, null)!;
        Assert.Null(res.VCmb);
        Assert.Null(res.PeculiarKms);
        Assert.Equal(Cosmo.LookbackTimeGyr(Cosmo.RedshiftAtComovingDistance(100)), res.LookbackGyr, 9);
    }

    [Fact]
    public void Resolver_UsesCmbFrameAtLowZ()
    {
        var (ra, dec) = FromGalactic(264.0, 48.3);
        var res = _r.Resolve(ra, dec, null, null, 1000, null)!;
        Assert.Equal(DistanceSource.Redshift, res.Source);
        Assert.Equal(1369.8, res.VCmb!.Value, 1);
        Assert.Equal(Cosmo.ComovingDistanceMpc(1369.8 / RedshiftMath.C), res.DistanceMpc, 2);
        Assert.Null(res.PeculiarKms);
    }

    [Fact]
    public void Resolver_UsesLocalGroupWhenCmbNonPositive()
    {
        var (ra, dec) = FromGalactic(84, -48.3);
        var res = _r.Resolve(ra, dec, null, null, 300, null)!;
        double expected = Cosmo.ComovingDistanceMpc(498.48 / RedshiftMath.C);
        Assert.Equal(expected, res.DistanceMpc, expected * 0.005);
        Assert.Equal(DistanceSource.Redshift, res.Source);
    }

    [Fact]
    public void DistanceResolver_ExcludesNonPositive()
    {
        var (ra1, dec1) = FromGalactic(90, 0);
        Assert.Null(_r.Resolve(ra1, dec1, null, null, -400, null));
        var (ra2, dec2) = FromGalactic(270, 0);
        Assert.Null(_r.Resolve(ra2, dec2, null, null, -400, null));
        Assert.Null(_r.Resolve(10, 10, null, null, null, null));
        // Invalid measured distances are ignored, not returned as 0 or NaN.
        Assert.Null(_r.Resolve(10, 10, 0, "X", null, null));
        Assert.Null(_r.Resolve(10, 10, double.NaN, "X", null, null));
    }

    [Fact]
    public void Resolver_HighZUsesComoving()
    {
        var res = _r.Resolve(150, 2, null, null, null, 1.0)!;
        Assert.Equal(3303.8, res.DistanceMpc, 3.3);
        Assert.Equal(DistanceSource.Redshift, res.Source);
        Assert.Null(res.PeculiarKms);
        Assert.Equal(Cosmo.LookbackTimeGyr(1.0), res.LookbackGyr, 9);
    }

    [Fact]
    public void Resolver_MeasuredLookbackUsesDistanceNotObservedRedshift()
    {
        var res = _r.Resolve(10.68, 41.27, 0.77, "UNGC", -300, null)!;
        double expected = Cosmo.LookbackTimeGyr(Cosmo.RedshiftAtComovingDistance(0.77));
        Assert.True(res.LookbackGyr > 0);
        Assert.Equal(expected, res.LookbackGyr, 1e-12);
    }

    // Ruling R38: the peculiar velocity follows the convention of the catalog that gave the distance.

    [Theory]
    [InlineData("UNGC")]
    [InlineData("McConnachie 2012")]
    public void Peculiar_LocalVolume_UsesLocalGroupFrameAndH73(string catalog)
    {
        // M31: cz -300 km/s at 0.77 Mpc. In the Local Group frame it approaches at about 120 km/s.
        var res = _r.Resolve(10.6847, 41.2690, 0.77, catalog, -300, null)!;
        Assert.InRange(res.PeculiarKms!.Value, -160, -80);
        var (l, b) = SkyMath.ToSpherical(SkyFrames.EquatorialToGalactic * SkyMath.UnitVector(10.6847, 41.2690));
        Assert.Equal(RedshiftMath.HelioToLocalGroup(-300, l, b) - 73 * 0.77, res.PeculiarKms.Value, 9);
    }

    [Fact]
    public void Peculiar_Cosmicflows4_UsesItsOwnH0()
    {
        var res = _r.Resolve(150, 20, 50, PeculiarVelocityRule.Cosmicflows4, 4000, null)!;
        Assert.Equal(res.VCmb!.Value - 74.6 * 50, res.PeculiarKms!.Value, 9);
    }

    [Fact]
    public void Peculiar_Cosmicflows4_BeyondLimit_IsNull()
    {
        var res = _r.Resolve(150, 20, 151, PeculiarVelocityRule.Cosmicflows4, 11000, null)!;
        Assert.NotNull(res.VCmb);
        Assert.Null(res.PeculiarKms);
        Assert.True(PeculiarVelocityRule.IsUnreliable(PeculiarVelocityRule.Cosmicflows4, 151));
        Assert.False(PeculiarVelocityRule.IsUnreliable(PeculiarVelocityRule.Cosmicflows4, 150));
        Assert.False(PeculiarVelocityRule.IsUnreliable("UNGC", 151));
    }

    [Fact]
    public void Peculiar_UnknownCatalog_IsNull()
    {
        Assert.Null(_r.Resolve(150, 20, 50, "Somewhere", 4000, null)!.PeculiarKms);
        Assert.Equal(PeculiarFrame.None, PeculiarVelocityRule.FrameOf("Somewhere"));
        Assert.Equal(PeculiarFrame.LocalGroup, PeculiarVelocityRule.FrameOf("UNGC"));
        Assert.Equal(PeculiarFrame.LocalGroup, PeculiarVelocityRule.FrameOf("McConnachie 2012"));
        Assert.Equal(PeculiarFrame.CosmicMicrowaveBackground, PeculiarVelocityRule.FrameOf("Cosmicflows-4"));
    }
}
