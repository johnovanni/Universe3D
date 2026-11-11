namespace Universe3D.Core.Coordinates;

public static class SkyMath
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>Unit vector for longitude/latitude (RA/Dec, l/b or SGL/SGB) in degrees.</summary>
    public static Vector3d UnitVector(double raDeg, double decDeg)
    {
        double ra = raDeg * DegToRad, dec = decDeg * DegToRad;
        double cd = Math.Cos(dec);
        return new Vector3d(cd * Math.Cos(ra), cd * Math.Sin(ra), Math.Sin(dec));
    }

    /// <summary>Longitude in [0, 360) and latitude in [-90, 90], degrees.</summary>
    public static (double lonDeg, double latDeg) ToSpherical(Vector3d v)
    {
        double lon = Math.Atan2(v.Y, v.X) * RadToDeg;
        if (lon < 0) lon += 360.0;
        if (lon >= 360.0) lon -= 360.0;
        double lat = Math.Atan2(v.Z, Math.Sqrt(v.X * v.X + v.Y * v.Y)) * RadToDeg;
        return (lon, lat);
    }

    /// <summary>Great-circle separation in degrees (robust for small and large angles).</summary>
    public static double AngularSeparationDeg(double ra1, double dec1, double ra2, double dec2)
    {
        var a = UnitVector(ra1, dec1);
        var b = UnitVector(ra2, dec2);
        return Math.Atan2(a.Cross(b).Length, a.Dot(b)) * RadToDeg;
    }
}
