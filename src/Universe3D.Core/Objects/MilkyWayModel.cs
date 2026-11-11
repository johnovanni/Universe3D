using Universe3D.Core.Coordinates;

namespace Universe3D.Core.Objects;

/// <summary>Where the Milky Way's center and disk are, in equatorial coordinates with the Sun at the origin.</summary>
public static class MilkyWayModel
{
    public const double DiameterMpc = 0.030;
    private const double CenterDistanceMpc = 0.0082;

    private static readonly Matrix3d GalacticToEquatorial = SkyFrames.EquatorialToGalactic.Transpose();

    /// <summary>8.2 kpc toward galactic l = 0, b = 0, in Mpc.</summary>
    public static Vector3d CenterEq { get; } = GalacticToEquatorial * new Vector3d(1, 0, 0) * CenterDistanceMpc;

    /// <summary>Unit vector of the galactic north pole.</summary>
    public static Vector3d NormalEq { get; } = GalacticToEquatorial * new Vector3d(0, 0, 1);
}
