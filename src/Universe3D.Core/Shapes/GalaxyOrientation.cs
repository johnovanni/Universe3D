using System.Numerics;
using Universe3D.Core.Coordinates;

namespace Universe3D.Core.Shapes;

/// <summary>
/// Orientation of a galaxy disk in 3D from its on-sky position angle (degrees east of north)
/// and axis ratio. Sign convention: the disk normal is tilted toward +north.
/// </summary>
public static class GalaxyOrientation
{
    private const double Deg = Math.PI / 180.0;

    /// <summary>Inclination (deg) of a disk of intrinsic thickness q0 seen with axis ratio q; 90 when q &lt;= q0.</summary>
    public static double InclinationDeg(double axisRatio, double q0 = 0.2)
    {
        if (axisRatio <= q0) return 90.0;
        var cos2 = (axisRatio * axisRatio - q0 * q0) / (1.0 - q0 * q0);
        return Math.Acos(Math.Sqrt(Math.Min(1.0, cos2))) / Deg;
    }

    /// <summary>Local sky basis: line of sight s (away from the Sun), east e, north n.</summary>
    private static (Vector3d s, Vector3d e, Vector3d n) Basis(double raDeg, double decDeg)
    {
        var s = SkyMath.UnitVector(raDeg, decDeg);
        // normalize(Cross(z, s)) = (-sin ra, cos ra, 0); also a consistent choice at the poles.
        var e = new Vector3d(-Math.Sin(raDeg * Deg), Math.Cos(raDeg * Deg), 0);
        var n = s.Cross(e);
        return (s, e, n);
    }

    /// <summary>Unit disk normal (equatorial), with normal·north >= 0 and pointing toward the observer.</summary>
    public static Vector3d DiskNormal(double raDeg, double decDeg, double paDeg, double inclinationDeg)
    {
        var (s, e, n) = Basis(raDeg, decDeg);
        var pa = paDeg * Deg;
        var minor = e * Math.Cos(pa) - n * Math.Sin(pa); // sky direction perpendicular to the major axis
        if (minor.Dot(n) < 0) minor = -minor;
        var i = inclinationDeg * Deg;
        return (minor * Math.Sin(i) - s * Math.Cos(i)).Normalized();
    }

    /// <summary>Inverse of <see cref="DiskNormal"/>: PA in [0, 180) and observed axis ratio.</summary>
    public static (double paDeg, double axisRatio) ProjectToSky(double raDeg, double decDeg, Vector3d normal, double q0 = 0.2)
    {
        var (s, e, n) = Basis(raDeg, decDeg);
        var cosI = Math.Abs(normal.Dot(s));
        var q = Math.Sqrt(cosI * cosI * (1.0 - q0 * q0) + q0 * q0);

        var a = normal.Dot(e);
        var b = normal.Dot(n);
        double pa = 0;
        if (a * a + b * b > 1e-18)
        {
            pa = Math.Atan2(a, b) / Deg + 90.0; // major axis is perpendicular to the projected normal
            pa %= 180.0;
            if (pa < 0) pa += 180.0;
        }
        return (pa, q);
    }

    /// <summary>Rotation mapping local +Z to the disk normal and local +X to the on-sky major axis.</summary>
    public static Quaternion DiskRotation(Vector3d normal, double paDeg, double raDeg, double decDeg)
    {
        var (_, e, n) = Basis(raDeg, decDeg);
        var pa = paDeg * Deg;
        var z = normal.Normalized();
        var major = n * Math.Cos(pa) + e * Math.Sin(pa);
        var x = (major - z * major.Dot(z)).Normalized(); // lies in the disk plane
        var y = z.Cross(x);

        // System.Numerics uses row vectors: row k is the image of local axis k.
        var m = new Matrix4x4(
            (float)x.X, (float)x.Y, (float)x.Z, 0,
            (float)y.X, (float)y.Y, (float)y.Z, 0,
            (float)z.X, (float)z.Y, (float)z.Z, 0,
            0, 0, 0, 1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }
}
