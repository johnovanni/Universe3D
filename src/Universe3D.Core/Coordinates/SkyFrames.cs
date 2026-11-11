namespace Universe3D.Core.Coordinates;

public enum DisplayFrame { Supergalactic, Galactic, Equatorial }

/// <summary>
/// Rotation matrices between sky frames. Each matrix has as rows the target frame's
/// x, y and z axes expressed in the source frame.
/// </summary>
public static class SkyFrames
{
    // IAU galactic frame: north pole RA/Dec, and l of the north celestial pole.
    private const double GalNorthRaDeg = 192.85948;
    private const double GalNorthDecDeg = 27.12825;
    private const double GalNcpLonDeg = 122.93192;

    // Supergalactic: north pole at galactic (47.37, 6.32); SGL = 0 at galactic (137.37, 0).
    private const double SgNorthLDeg = 47.37;
    private const double SgNorthBDeg = 6.32;
    private const double SgZeroLDeg = 137.37;

    public static Matrix3d EquatorialToGalactic { get; } = BuildEquatorialToGalactic();
    public static Matrix3d GalacticToSupergalactic { get; } = BuildGalacticToSupergalactic();

    private static readonly Matrix3d EquatorialToSupergalactic = GalacticToSupergalactic * EquatorialToGalactic;

    public static Matrix3d EquatorialTo(DisplayFrame frame) => frame switch
    {
        DisplayFrame.Equatorial => Matrix3d.Identity,
        DisplayFrame.Galactic => EquatorialToGalactic,
        DisplayFrame.Supergalactic => EquatorialToSupergalactic,
        _ => throw new ArgumentOutOfRangeException(nameof(frame)),
    };

    private static Matrix3d BuildEquatorialToGalactic()
    {
        // z axis: galactic north pole. x axis: toward l=0,b=0, i.e. perpendicular to z and
        // in the plane containing z and the north celestial pole, at galactic longitude 0.
        var z = SkyMath.UnitVector(GalNorthRaDeg, GalNorthDecDeg);
        var ncp = new Vector3d(0, 0, 1);
        // Direction in galactic plane toward l = GalNcpLon is the projection of NCP onto the plane.
        var toNcp = (ncp - z * ncp.Dot(z)).Normalized();   // in-plane, at l = 122.93192
        var perp = z.Cross(toNcp);                         // in-plane, at l = 122.93192 + 90
        double a = GalNcpLonDeg * Math.PI / 180.0;
        // x (l=0) = cos(a)*toNcp - sin(a)*perp
        var x = (toNcp * Math.Cos(a) - perp * Math.Sin(a)).Normalized();
        var y = z.Cross(x);
        return Matrix3d.FromRows(x, y, z);
    }

    private static Matrix3d BuildGalacticToSupergalactic()
    {
        var z = SkyMath.UnitVector(SgNorthLDeg, SgNorthBDeg);
        var zero = SkyMath.UnitVector(SgZeroLDeg, 0);
        // Remove any residual component along z so the frame is exactly orthonormal.
        var x = (zero - z * zero.Dot(z)).Normalized();
        var y = z.Cross(x);
        return Matrix3d.FromRows(x, y, z);
    }
}
