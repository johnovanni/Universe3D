using System.Globalization;

namespace Universe3D.Core.Display;

/// <summary>
/// Scale rings around the Sun in the display frame's reference plane, at 1-3-10 spacing (1 Mly, 3 Mly, 10 Mly, ...),
/// for the decades around the current zoom level.
/// </summary>
public static class ScaleRings
{
    private static readonly string[] Units = { "ly", "kly", "Mly", "Gly" };

    /// <summary>Every r in {1, 3}·10ⁿ ly with d/20 ≤ r ≤ 2d, ascending; empty for a non-positive or non-finite d.</summary>
    public static IReadOnlyList<double> RadiiLy(double cameraDistanceLy)
    {
        double d = cameraDistanceLy;
        if (!(d > 0) || !double.IsFinite(d)) return Array.Empty<double>();
        double lo = d / 20, hi = 2 * d;
        const double tolerance = 1e-12; // keep the inclusive bounds exact despite rounding
        var radii = new List<double>(4);
        for (int n = (int)Math.Floor(Math.Log10(lo)) - 1; n <= (int)Math.Ceiling(Math.Log10(hi)); n++)
        {
            double decade = Pow10(n);
            foreach (double r in new[] { decade, 3 * decade })
                if (r >= lo * (1 - tolerance) && r <= hi * (1 + tolerance)) radii.Add(r);
        }
        return radii;
    }

    /// <summary>A ring label such as "30 ly", "300 kly", "1 Mly" or "10 Gly".</summary>
    public static string Label(double radiusLy)
    {
        int u = radiusLy < 1e3 ? 0 : radiusLy < 1e6 ? 1 : radiusLy < 1e9 ? 2 : 3;
        double v = radiusLy / Pow10(3 * u);
        return v.ToString("G3", CultureInfo.InvariantCulture) + " " + Units[u];
    }

    // Exact for n >= 0 (the camera is never closer than 1e-4 Mpc, about 326 ly).
    private static double Pow10(int n) => Math.Pow(10, n);
}
