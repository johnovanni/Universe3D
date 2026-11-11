using System.Globalization;
using Universe3D.Core.Coordinates;

namespace Universe3D.Core.Cosmology;

public static class RedshiftMath
{
    public const double C = 299792.458;

    private const double CmbDipoleKms = 369.8;
    private const double CmbApexL = 264.0, CmbApexB = 48.3;
    private const double LocalGroupKms = 300.0;
    private const double H0 = 70.0;
    /// <summary>Above this redshift the shift is described as cosmic expansion and peculiar velocities are not computed.</summary>
    public const double CosmicExpansionZ = 0.1;

    private static readonly Vector3d CmbApex = SkyMath.UnitVector(CmbApexL, CmbApexB);

    /// <summary>Heliocentric to CMB frame; (l, b) are galactic degrees.</summary>
    public static double HelioToCmb(double czHelio, double lDeg, double bDeg)
        => czHelio + CmbDipoleKms * SkyMath.UnitVector(lDeg, bDeg).Dot(CmbApex);

    /// <summary>Heliocentric to Local Group frame: v + 300 sin(l) cos(b).</summary>
    public static double HelioToLocalGroup(double czHelio, double lDeg, double bDeg)
    {
        double l = lDeg * Math.PI / 180.0, b = bDeg * Math.PI / 180.0;
        return czHelio + LocalGroupKms * Math.Sin(l) * Math.Cos(b);
    }

    /// <summary>v_pec = v_CMB - H0 d; null when z is too large for the linear Hubble law (z &gt;= 0.1).</summary>
    public static double? PeculiarVelocity(double vCmb, double distanceMpc, double z)
        => z >= CosmicExpansionZ ? null : vCmb - H0 * distanceMpc;

    public static string ShiftDescription(double czHelio, double z)
    {
        if (z > CosmicExpansionZ)
            return string.Create(CultureInfo.InvariantCulture,
                $"Redshifted: recession due to cosmic expansion (z = {z.ToString("G3", CultureInfo.InvariantCulture)})");
        string speed = Math.Abs(czHelio).ToString("N0", CultureInfo.InvariantCulture);
        return czHelio < 0
            ? $"Blueshifted: approaching at {speed} km/s"
            : $"Redshifted: receding at {speed} km/s";
    }
}
