using Universe3D.Core.Cosmology;

namespace Universe3D.Core.Catalogs;

/// <summary>The reference frame a peculiar velocity is measured in.</summary>
public enum PeculiarFrame { None, LocalGroup, CosmicMicrowaveBackground }

/// <summary>
/// Ruling R38: a peculiar velocity follows the convention of the catalog that supplied the distance.
/// <list type="bullet">
/// <item>Cosmicflows-4: <c>v_pec = v_CMB − 74.6·d</c>, CF4's own distance zero point (Tully+ 2023), and only
/// for d ≤ 150 Mpc; farther out a single CF4 distance is too uncertain (about 20%).</item>
/// <item>Local Volume catalogs (UNGC, McConnachie): Local Group frame, <c>v_pec = v_LG − 73·d</c>
/// (the Karachentsev convention), so the Local Group's own motion is not counted.</item>
/// </list>
/// Other catalogs have no convention here, so they get no peculiar velocity.
/// </summary>
public static class PeculiarVelocityRule
{
    public const string Cosmicflows4 = "Cosmicflows-4";
    public const string Ungc = "UNGC";
    public const string McConnachie = "McConnachie 2012";

    /// <summary>Hubble constant matching the Cosmicflows-4 distance scale, km/s/Mpc.</summary>
    public const double Cf4H0 = 74.6;

    /// <summary>Hubble constant of the Local Volume convention, km/s/Mpc.</summary>
    public const double LocalVolumeH0 = 73.0;

    /// <summary>Cosmicflows-4 peculiar velocities are given only up to this distance, Mpc.</summary>
    public const double Cf4MaxMpc = 150;

    public static PeculiarFrame FrameOf(string? distanceCatalog) => distanceCatalog switch
    {
        Cosmicflows4 => PeculiarFrame.CosmicMicrowaveBackground,
        Ungc or McConnachie => PeculiarFrame.LocalGroup,
        _ => PeculiarFrame.None,
    };

    /// <summary>True when the catalog has a convention but the distance is too large for a reliable value.</summary>
    public static bool IsUnreliable(string? distanceCatalog, double distanceMpc) =>
        FrameOf(distanceCatalog) == PeculiarFrame.CosmicMicrowaveBackground && distanceMpc > Cf4MaxMpc;

    /// <summary>
    /// The radial peculiar velocity in km/s, or null when the catalog has no convention or the distance is beyond
    /// its limit. (<paramref name="lDeg"/>, <paramref name="bDeg"/>) are galactic degrees.
    /// </summary>
    public static double? Compute(string? distanceCatalog, double distanceMpc, double czHelio, double lDeg, double bDeg)
    {
        if (IsUnreliable(distanceCatalog, distanceMpc)) return null;
        return FrameOf(distanceCatalog) switch
        {
            PeculiarFrame.CosmicMicrowaveBackground => RedshiftMath.HelioToCmb(czHelio, lDeg, bDeg) - Cf4H0 * distanceMpc,
            PeculiarFrame.LocalGroup => RedshiftMath.HelioToLocalGroup(czHelio, lDeg, bDeg) - LocalVolumeH0 * distanceMpc,
            _ => null,
        };
    }
}
