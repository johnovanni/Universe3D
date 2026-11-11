using Universe3D.Core.Catalogs;

namespace Universe3D.Tests;

public class SkyCrossMatcherTests
{
    [Fact]
    public void CrossMatch_FindsPairs()
    {
        var rnd = new Random(42);
        var pts = new List<(double ra, double dec)>();
        var m = new SkyCrossMatcher();
        for (int i = 0; i < 1000; i++)
        {
            double ra = rnd.NextDouble() * 360;
            double dec = Math.Asin(rnd.NextDouble() * 2 - 1) * 180 / Math.PI;
            pts.Add((ra, dec));
            m.Add(ra, dec, i);
        }
        for (int i = 0; i < pts.Count; i++)
        {
            var (ra, dec) = pts[i];
            double cos = Math.Max(0.01, Math.Cos(dec * Math.PI / 180));
            // 10 arcsec on the sky, split between Dec and RA.
            double dRa = (10.0 / 3600 / cos) * Math.Cos(Math.PI / 4);
            double dDec = (10.0 / 3600) * Math.Sin(Math.PI / 4);
            double near = Math.Min(89.9999, dec + dDec);
            Assert.Equal(i, m.FindNearest(ra + dRa, near));
            double far = dec > 0 ? dec - 40.0 / 3600 : dec + 40.0 / 3600;
            Assert.Equal(-1, m.FindNearest(ra, far));
        }
    }

    [Fact]
    public void CrossMatch_WrapsRa()
    {
        var m = new SkyCrossMatcher();
        m.Add(0.0005, 0, 7);
        Assert.Equal(7, m.FindNearest(359.9990, 0));
    }

    [Fact]
    public void CrossMatch_NearPole()
    {
        var m = new SkyCrossMatcher();
        m.Add(190, 89.999, 3);
        Assert.Equal(3, m.FindNearest(10, 89.999));
    }
}
