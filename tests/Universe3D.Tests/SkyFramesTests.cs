using System.Globalization;
using Universe3D.Core.Coordinates;

namespace Universe3D.Tests;

public class SkyFramesTests
{
    private const double Tol = 0.001;

    private static double Wrap180(double deg)
    {
        deg %= 360;
        if (deg <= -180) deg += 360;
        if (deg > 180) deg -= 360;
        return deg;
    }

    [Fact]
    public void GalacticNorthPole_MapsToB90()
    {
        var v = SkyFrames.EquatorialToGalactic * SkyMath.UnitVector(192.85948, 27.12825);
        var (_, lat) = SkyMath.ToSpherical(v);
        Assert.InRange(lat, 90 - Tol, 90 + Tol);
    }

    [Fact]
    public void GalacticOrigin_MapsToZeroZero()
    {
        var v = SkyFrames.EquatorialToGalactic * SkyMath.UnitVector(266.40499, -28.93617);
        var (lon, lat) = SkyMath.ToSpherical(v);
        Assert.InRange(Wrap180(lon), -Tol, Tol);
        Assert.InRange(lat, -Tol, Tol);
    }

    [Fact]
    public void SupergalacticPole_MapsToSgb90()
    {
        var v = SkyFrames.GalacticToSupergalactic * SkyMath.UnitVector(47.37, 6.32);
        var (_, lat) = SkyMath.ToSpherical(v);
        Assert.InRange(lat, 90 - Tol, 90 + Tol);
    }

    [Fact]
    public void SupergalacticZero_MapsToSgl0()
    {
        var v = SkyFrames.GalacticToSupergalactic * SkyMath.UnitVector(137.37, 0);
        var (lon, lat) = SkyMath.ToSpherical(v);
        Assert.InRange(Wrap180(lon), -Tol, Tol);
        Assert.InRange(lat, -Tol, Tol);
    }

    [Fact]
    public void Cf4Sample_SupergalacticMatchesCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cf4_sample.tsv");
        var rows = File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .Where(c => c.Length >= 5 && double.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .ToList();
        Assert.True(rows.Count >= 100, $"fixture has only {rows.Count} data rows");

        foreach (var c in rows)
        {
            double Parse(int i) => double.Parse(c[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            double ra = Parse(1), dec = Parse(2), sgl = Parse(3), sgb = Parse(4);
            var v = SkyFrames.EquatorialTo(DisplayFrame.Supergalactic) * SkyMath.UnitVector(ra, dec);
            var (lon, lat) = SkyMath.ToSpherical(v);
            Assert.True(Math.Abs(Wrap180(lon - sgl)) < 0.01, $"PGC {c[0].Trim()} SGL {lon} vs {sgl}");
            Assert.True(Math.Abs(lat - sgb) < 0.01, $"PGC {c[0].Trim()} SGB {lat} vs {sgb}");
        }
    }

    [Fact]
    public void Matrices_AreOrthonormal()
    {
        foreach (var m in new[]
        {
            SkyFrames.EquatorialToGalactic,
            SkyFrames.GalacticToSupergalactic,
            SkyFrames.EquatorialTo(DisplayFrame.Supergalactic),
            SkyFrames.EquatorialTo(DisplayFrame.Galactic),
            SkyFrames.EquatorialTo(DisplayFrame.Equatorial),
        })
        {
            var p = m * m.Transpose();
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    Assert.InRange(p[r, c], (r == c ? 1 : 0) - 1e-12, (r == c ? 1 : 0) + 1e-12);
        }
    }
}
