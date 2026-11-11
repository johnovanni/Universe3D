using System.Numerics;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Shapes;
using Xunit;

namespace Universe3D.Tests;

public class GalaxyOrientationTests
{
    [Theory]
    [InlineData(1.0, 0.0, 0.01)]
    [InlineData(0.2, 90.0, 0.01)]
    [InlineData(0.1, 90.0, 0.01)]
    [InlineData(0.5, 62.11, 0.01)]
    public void Inclination_Values(double q, double expected, double tol)
        => Assert.Equal(expected, GalaxyOrientation.InclinationDeg(q), tol);

    [Theory]
    [InlineData(10.68, 41.27, 0, 0.3)]
    [InlineData(10.68, 41.27, 37, 0.6)]
    [InlineData(10.68, 41.27, 90, 0.9)]
    [InlineData(10.68, 41.27, 145, 0.3)]
    [InlineData(187.7, 12.4, 0, 0.9)]
    [InlineData(187.7, 12.4, 37, 0.3)]
    [InlineData(187.7, 12.4, 90, 0.6)]
    [InlineData(187.7, 12.4, 145, 0.9)]
    [InlineData(187.7, 12.4, 145, 0.6)]
    [InlineData(10.68, 41.27, 90, 0.3)]
    public void RoundTrip_ReproducesPaAndQ(double ra, double dec, double pa, double q)
    {
        var incl = GalaxyOrientation.InclinationDeg(q);
        var normal = GalaxyOrientation.DiskNormal(ra, dec, pa, incl);
        Assert.Equal(1.0, normal.Length, 1e-9);
        var (paOut, qOut) = GalaxyOrientation.ProjectToSky(ra, dec, normal);
        var dPa = Math.Abs(paOut - pa) % 180.0;
        dPa = Math.Min(dPa, 180.0 - dPa);
        Assert.True(dPa < 0.5, $"PA {paOut} vs {pa}");
        Assert.InRange(paOut, 0.0, 180.0 - 1e-12);
        Assert.Equal(q, qOut, 0.01);
    }

    [Theory]
    [InlineData(10.68, 41.27, 37, 45)]
    [InlineData(187.7, 12.4, 120, 70)]
    [InlineData(187.7, 12.4, 0, 30)]
    [InlineData(10.68, 41.27, 90, 60)]
    public void Normal_TiltsTowardNorth(double ra, double dec, double pa, double incl)
    {
        var normal = GalaxyOrientation.DiskNormal(ra, dec, pa, incl);
        var s = SkyMath.UnitVector(ra, dec);
        var e = new Vector3d(-Math.Sin(ra * Math.PI / 180), Math.Cos(ra * Math.PI / 180), 0);
        var n = s.Cross(e);
        Assert.True(normal.Dot(n) >= -1e-12);
    }

    [Theory]
    [InlineData(10.68, 41.27, 37, 0.4)]
    [InlineData(187.7, 12.4, 145, 0.7)]
    [InlineData(0, 90, 20, 0.5)]
    public void DiskRotation_MapsZToNormalAndXToMajorAxis(double ra, double dec, double pa, double q)
    {
        var normal = GalaxyOrientation.DiskNormal(ra, dec, pa, GalaxyOrientation.InclinationDeg(q));
        var rot = GalaxyOrientation.DiskRotation(normal, pa, ra, dec);

        var s = SkyMath.UnitVector(ra, dec);
        var e = new Vector3d(-Math.Sin(ra * Math.PI / 180), Math.Cos(ra * Math.PI / 180), 0);
        var n = s.Cross(e);
        var p = pa * Math.PI / 180;
        var major = n * Math.Cos(p) + e * Math.Sin(p);

        var z = Vector3.Transform(new Vector3(0, 0, 1), rot);
        var x = Vector3.Transform(new Vector3(1, 0, 0), rot);
        Assert.True(Math.Abs(z.X - normal.X) < 1e-5 && Math.Abs(z.Y - normal.Y) < 1e-5 && Math.Abs(z.Z - normal.Z) < 1e-5, $"Z {z} vs {normal}");
        Assert.True(Math.Abs(x.X - major.X) < 1e-5 && Math.Abs(x.Y - major.Y) < 1e-5 && Math.Abs(x.Z - major.Z) < 1e-5, $"X {x} vs {major}");
    }
}
