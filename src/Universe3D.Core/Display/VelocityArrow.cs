using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;

namespace Universe3D.Core.Display;

/// <summary>Which velocity the arrows show; labeled "Observed" and "Peculiar velocity" in the UI.</summary>
public enum ArrowMode { Observed, PeculiarVelocity }

/// <summary>A velocity arrow: unit direction (same frame as the position), length in pixels, and color sense.</summary>
public sealed record ArrowSpec(Vector3d Direction, double LengthPx, bool Approaching);

/// <summary>
/// CPU mirror of <c>arrows.geom</c>: arrows lie along the Sun-to-object line, outward when receding (red)
/// and inward when approaching (blue), with length <c>L = clamp(k·log10(1 + |v|/100), 3, 60)</c> px.
/// </summary>
public static class VelocityArrow
{
    public const double DefaultK = 18;
    public const double MinLengthPx = 3;
    public const double MaxLengthPx = 60;

    /// <summary>Above this redshift the observed speed is c·z (cosmic expansion) rather than cz.</summary>
    public const double CosmicExpansionZ = 0.1;

    public static ArrowSpec? Compute(Vector3d positionEq, double? czHelio, double? z, double? pecKms,
                                     ArrowMode mode, double k = DefaultK)
    {
        double? v = mode == ArrowMode.Observed ? ObservedVelocity(czHelio, z) : pecKms;
        if (v is not double speed || double.IsNaN(speed)) return null;
        if (positionEq.Length == 0) return null;

        bool approaching = speed < 0;
        var radial = positionEq.Normalized();
        return new ArrowSpec(approaching ? -radial : radial, LengthPx(speed, k), approaching);
    }

    /// <summary>Signed observed velocity, km/s: c·z when z &gt; 0.1, else the heliocentric cz.</summary>
    public static double? ObservedVelocity(double? czHelio, double? z)
    {
        if (z is double zz && zz > CosmicExpansionZ) return RedshiftMath.C * zz;
        if (czHelio is double cz) return cz;
        return z is double zOnly ? RedshiftMath.C * zOnly : null;
    }

    public static double LengthPx(double speedKms, double k = DefaultK) =>
        Math.Clamp(k * Math.Log10(1 + Math.Abs(speedKms) / 100), MinLengthPx, MaxLengthPx);
}
