using System.Globalization;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;

namespace Universe3D.Core.Text;

/// <summary>One line of the information panel; <paramref name="GlossaryKey"/> names its tooltip, if any.</summary>
public sealed record InfoRow(string Label, string Value, string? GlossaryKey);

/// <summary>The information panel's rows for a selected object, in plain language (spec §3 and §5).</summary>
public static class ObjectInfoFormatter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Wording for galaxies the catalog does not classify (Ruling R19).</summary>
    public const string Unclassified = "Type not classified in the catalog";

    public static IReadOnlyList<InfoRow> Format(CosmicObject o, CosmologyModel cosmo)
    {
        var rows = new List<InfoRow>();
        double? cz = o.CzHelio ?? o.Z * RedshiftMath.C;
        double? z = o.Z ?? o.CzHelio / RedshiftMath.C;
        bool cosmic = z > RedshiftMath.CosmicExpansionZ;
        bool measured = o.DistanceSource == DistanceSource.Measured;
        var lookback = new InfoRow("Look-back time", FormatLookback(LookbackGyr(o, z, cosmo)), "LookbackTime");

        // Far away, how long ago the light left is the headline figure.
        if (cosmic) rows.Add(lookback);

        var aka = o.AltNames.Where(a => !string.Equals(a, o.Name, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (aka.Count > 0) rows.Add(new("Also known as", string.Join(", ", aka), null));

        switch (o)
        {
            case Galaxy g:
                AddGalaxy(rows, g);
                break;
            case GalaxyCluster:
                rows.Add(new("Type", "Galaxy cluster", null));
                break;
            case Quasar q:
                rows.Add(QuasarType(q.Type));
                break;
        }

        if (cz is double v && z is double zz)
        {
            rows.Add(new("Motion", RedshiftMath.ShiftDescription(v, zz), v < 0 ? "Blueshift" : "Redshift"));
            rows.Add(new("Redshift z", FormatZ(zz), "Redshift"));
            rows.Add(new("cz (heliocentric)",
                Kms(v) + (cosmic ? " (the redshift written as a speed; not a real speed this far away)" : ""), "Cz"));
            if (!cosmic && o.VCmb is double cmb) rows.Add(new("CMB-frame velocity", Kms(cmb), "CmbFrame"));
            if (!cosmic && measured && PeculiarRow(o) is { } pecRow) rows.Add(pecRow);
            rows.Add(new("Velocity arrow", "Line-of-sight motion only", "LineOfSightOnly"));
        }
        else
        {
            rows.Add(new("Motion", "No velocity measured", null));
        }

        rows.Add(new("Distance",
            Units.FormatDistance(o.DistanceMpc) + (measured ? $", measured ({o.DistanceCatalog})" : ", from redshift"),
            measured ? "MeasuredDistance" : "ComovingDistance"));
        if (!cosmic) rows.Add(lookback);

        switch (o)
        {
            case GalaxyCluster c:
                AddCluster(rows, c);
                break;
            case Quasar q:
                if (q.RMag is double r) rows.Add(new("R magnitude", r.ToString("0.0#", Inv), "Magnitude"));
                if (q.BMag is double b) rows.Add(new("B magnitude", b.ToString("0.0#", Inv), "Magnitude"));
                break;
        }

        if (o.Sources.Length > 0) rows.Add(new("Data sources", string.Join(", ", o.Sources), null));
        rows.Add(new("Catalog id", o.Id, null));
        return rows;
    }

    /// <summary>
    /// Ruling R38: the peculiar velocity names its frame (Local Group for Local Volume distances, the cosmic microwave
    /// background for Cosmicflows-4); beyond the Cosmicflows-4 limit it is "not reliable at this distance".
    /// </summary>
    private static InfoRow? PeculiarRow(CosmicObject o)
    {
        if (o.PeculiarKms is double pec)
        {
            string frame = PeculiarVelocityRule.FrameOf(o.DistanceCatalog) switch
            {
                PeculiarFrame.LocalGroup => ", relative to the Local Group",
                PeculiarFrame.CosmicMicrowaveBackground => ", relative to the cosmic microwave background",
                _ => "",
            };
            return new("Peculiar velocity",
                $"{Kms(Math.Abs(pec))} {(pec < 0 ? "toward us" : "away from us")}{frame}, with the expansion removed",
                "PeculiarVelocity");
        }
        return PeculiarVelocityRule.IsUnreliable(o.DistanceCatalog, o.DistanceMpc)
            ? new("Peculiar velocity", "not reliable at this distance", "PeculiarVelocity")
            : null;
    }

    /// <summary>Ruling R8: a measured distance gives the light-travel time; otherwise the redshift does.</summary>
    private static double LookbackGyr(CosmicObject o, double? z, CosmologyModel cosmo) =>
        o.DistanceSource == DistanceSource.Measured || z is not double zz
            ? cosmo.LookbackTimeGyr(cosmo.RedshiftAtComovingDistance(o.DistanceMpc))
            : cosmo.LookbackTimeGyr(zz);

    private static void AddGalaxy(List<InfoRow> rows, Galaxy g)
    {
        rows.Add(new("Type", TypeText(g.Morphology), "HubbleType"));
        rows.Add(new("Position angle", g.PaDeg is double pa ? Deg(pa) : "not measured", "PositionAngle"));
        rows.Add(new("Axis ratio (b/a)", g.AxisRatio is double q ? q.ToString("0.00", Inv) : "not measured", "AxisRatio"));
        rows.Add(new("Inclination",
            g.InclinationDeg is double i
                ? Deg(i) + (g.InclinationMeasured ? "" : " (estimated from the axis ratio)")
                : "not measured",
            "Inclination"));
        if (g.DiameterKpc is double kpc)
            rows.Add(new("Diameter", $"{Units.FormatLightYears(kpc / 1000)} ({kpc.ToString("0.#", Inv)} kpc)", "Diameter"));

        bool disk = g.Morphology.Class is not (MorphologyClass.Elliptical or MorphologyClass.DwarfSpheroidal);
        // Ruling R27: without a measured position angle the drawn orientation is made up.
        if (g.PaDeg is null)
            rows.Add(new(disk ? "Disk tilt" : "Orientation",
                $"The drawn {(disk ? "tilt" : "orientation")} is illustrative: the position angle was not measured",
                disk ? "DiskTilt" : null));
        else if (disk)
            rows.Add(new("Disk tilt",
                "Drawn with its north edge tilted away from us (tilt direction assumed: images cannot show which edge is nearer)",
                "DiskTilt"));
    }

    private static string TypeText(MorphologyInfo m)
    {
        string code = m.OriginalCode.Trim();
        bool hasCode = code.Length > 0 && code.Any(char.IsLetterOrDigit);
        string name = m.Class == MorphologyClass.Unknown ? Unclassified : Capitalize(m.Description);
        return hasCode ? $"{name} (catalog code {code})" : name;
    }

    private static InfoRow QuasarType(QuasarType t) => t switch
    {
        Objects.QuasarType.Quasar => new("Type", "Quasar", "Quasar"),
        Objects.QuasarType.Agn => new("Type", "AGN (active galactic nucleus)", "Agn"),
        Objects.QuasarType.BlLac => new("Type", "BL Lac object", "BlLac"),
        _ => new("Type", "Type 2 AGN (hidden core)", "Type2Agn"),
    };

    private static void AddCluster(List<InfoRow> rows, GalaxyCluster c)
    {
        if (c.M500e14 is double m) rows.Add(new("Mass M500", $"{m.ToString("G3", Inv)} × 10¹⁴ solar masses", "M500"));
        if (c.R500Mpc is double r) rows.Add(new("Radius R500", Units.FormatDistance(r), "R500"));
        if (c.L500e37W is double l) rows.Add(new("X-ray luminosity L500", $"{l.ToString("G3", Inv)} × 10³⁷ W", "L500"));
        if (c.Richness is int rich) rows.Add(new("Richness class", rich.ToString(Inv), "Richness"));
        if (!string.IsNullOrWhiteSpace(c.BautzMorgan)) rows.Add(new("Bautz–Morgan type", c.BautzMorgan, "BautzMorgan"));
        string found = c.Catalogs switch
        {
            ClusterCatalogs.Mcxc => "MCXC (X-ray)",
            ClusterCatalogs.Abell => "Abell (optical)",
            _ => "MCXC (X-ray) and Abell (optical)",
        };
        rows.Add(new("Found by", found, c.Catalogs == ClusterCatalogs.Abell ? "Abell" : "Mcxc"));
    }

    /// <summary>"12.7 billion years", "2.51 million years", "163 thousand years".</summary>
    public static string FormatLookback(double gyr)
    {
        if (!(gyr > 0)) return "none (very close)";
        if (gyr >= 1) return gyr.ToString("G3", Inv) + " billion years";
        if (gyr >= 1e-3) return (gyr * 1e3).ToString("G3", Inv) + " million years";
        if (gyr >= 1e-6) return (gyr * 1e6).ToString("G3", Inv) + " thousand years";
        return "less than a thousand years";
    }

    private static string FormatZ(double z)
    {
        string s = z.ToString("G3", Inv);
        return s.Contains('E') ? z.ToString("0.000000##", Inv) : s;
    }

    private static string Kms(double v) => v.ToString("N0", Inv) + " km/s";

    private static string Deg(double d) => d.ToString("0", Inv) + "°";

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
