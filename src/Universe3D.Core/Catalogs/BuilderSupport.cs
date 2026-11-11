using System.Globalization;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using Universe3D.Core.Shapes;

namespace Universe3D.Core.Catalogs;

/// <summary>A galaxy's merged catalog values, before its distance is resolved.</summary>
internal sealed record GalaxyInput(
    string Id, IReadOnlyList<string?> Designations, double Ra, double Dec,
    double? MeasuredMpc, string? MeasuredCatalog, double? CzHelio, MorphologyInfo Morphology,
    double? PaDeg, double? AxisRatio, double? InclinationDeg, double? DiameterArcmin,
    double? Magnitude, string[] Sources);

/// <summary>Pieces shared by the data-set builders: the HyperLEDA query, positions, naming and galaxy assembly.</summary>
internal static class BuilderSupport
{
    public const string HyperLedaSource = "VII/237/pgc";

    public static readonly IReadOnlyList<string> HyperLedaColumns =
        ["PGC", "_RAJ2000", "_DEJ2000", "MType", "logD25", "logR25", "PA"];

    /// <summary>VizieR constraint <c>logD25=&gt;0.6</c>: galaxies larger than 0.4 arcmin.</summary>
    public static readonly IReadOnlyDictionary<string, string> HyperLedaConstraints =
        new Dictionary<string, string> { ["logD25"] = ">0.6" };

    private static readonly Dictionary<string, string> NgcToMessier =
        CommonNames.Messier.ToDictionary(kv => kv.Value, kv => "M " + kv.Key[1..]);

    /// <summary>
    /// Display name and alternate names from raw catalog designations. AltNames holds the normalised designations,
    /// the NGC equivalent of a Messier object (and vice versa), and the popular name when one exists. The display
    /// name is the popular name if any, else the best designation (Messier, then NGC, IC, UGC, then the rest).
    /// </summary>
    public static (string Name, string[] AltNames) Names(IEnumerable<string?> rawDesignations)
    {
        var names = new List<string>();
        void Add(string n)
        {
            if (n.Length > 0 && !names.Contains(n)) names.Add(n);
        }

        foreach (var raw in rawDesignations)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string n = CommonNames.NormalizeDesignation(raw);
            Add(n);
            if (n.StartsWith("M ", StringComparison.Ordinal)
                && CommonNames.Messier.TryGetValue("M" + n[2..], out var ngc)) Add(ngc);
            else if (NgcToMessier.TryGetValue(n, out var messier)) Add(messier);
        }

        var ordered = names.OrderBy(Rank).ToList(); // stable: ties keep catalog order
        string? popular = ordered.Select(n => CommonNames.Popular.TryGetValue(n, out var p) ? p : null)
            .FirstOrDefault(p => p != null);
        if (popular != null && !ordered.Contains(popular)) ordered.Add(popular);
        string name = popular ?? (ordered.Count > 0 ? ordered[0] : "");
        return (name, ordered.ToArray());
    }

    private static int Rank(string n) =>
        n.StartsWith("M ", StringComparison.Ordinal) ? 0
        : n.StartsWith("NGC ", StringComparison.Ordinal) ? 1
        : n.StartsWith("IC ", StringComparison.Ordinal) ? 2
        : n.StartsWith("UGC ", StringComparison.Ordinal) ? 3
        : n.StartsWith("PGC ", StringComparison.Ordinal) || n.StartsWith("2MASX", StringComparison.Ordinal) ? 5
        : 4;

    /// <summary>
    /// Physical diameter in kpc of an object <paramref name="arcmin"/> across at comoving distance
    /// <paramref name="distanceMpc"/>, using the angular-diameter distance D_A = D_C / (1 + z) (ruling R15).
    /// </summary>
    public static double? DiameterKpc(double? arcmin, double distanceMpc)
    {
        if (arcmin is not { } a || a <= 0) return null;
        double z = CosmologyModel.Default.RedshiftAtComovingDistance(distanceMpc);
        return distanceMpc / (1 + z) * 1000.0 * a / 60.0 * Math.PI / 180.0;
    }

    /// <summary>Decimal RA/Dec of a row, or null when either is missing.</summary>
    public static (double Ra, double Dec)? Position(TsvTable t, int row, string raCol, string decCol) =>
        t.GetDouble(row, raCol) is { } ra && t.GetDouble(row, decCol) is { } dec ? (ra, dec) : null;

    /// <summary>The galaxy, or null when the resolver finds no usable distance.</summary>
    public static Galaxy? MakeGalaxy(GalaxyInput g, DistanceResolver resolver)
    {
        var d = resolver.Resolve(g.Ra, g.Dec, g.MeasuredMpc, g.MeasuredCatalog, g.CzHelio, null);
        if (d == null) return null;
        var (name, alt) = Names(g.Designations);
        double? q = g.AxisRatio is { } r && r > 0 && r <= 1 ? r : null;
        double? incl = g.InclinationDeg ?? (q is { } qq ? GalaxyOrientation.InclinationDeg(qq) : null);
        return new Galaxy
        {
            Id = g.Id, Name = name, AltNames = alt, RaDeg = g.Ra, DecDeg = g.Dec,
            DistanceMpc = d.DistanceMpc, DistanceSource = d.Source, DistanceCatalog = d.Catalog,
            Z = g.CzHelio / RedshiftMath.C, CzHelio = g.CzHelio, VCmb = d.VCmb, PeculiarKms = d.PeculiarKms,
            LookbackGyr = d.LookbackGyr, Magnitude = g.Magnitude, Sources = g.Sources,
            Morphology = g.Morphology, PaDeg = g.PaDeg, AxisRatio = q,
            InclinationDeg = incl, InclinationMeasured = g.InclinationDeg != null,
            DiameterKpc = DiameterKpc(g.DiameterArcmin, d.DistanceMpc),
        };
    }

    /// <summary>HyperLEDA values of one row: D25 in arcmin, axis ratio from logR25, PA and morphology.</summary>
    public static (double? D25Arcmin, double? AxisRatio, double? PaDeg, MorphologyInfo Morphology) Leda(TsvTable leda, int row)
    {
        if (row < 0) return (null, null, null, MorphologyParser.FromHyperLeda(null));
        double? d25 = leda.GetDouble(row, "logD25") is { } ld ? 0.1 * Math.Pow(10, ld) : null;
        double? q = leda.GetDouble(row, "logR25") is { } lr ? Math.Pow(10, -lr) : null;
        return (d25, q, leda.GetDouble(row, "PA"), MorphologyParser.FromHyperLeda(leda.Get(row, "MType")));
    }

    /// <summary>"ra,dec" to 4 decimals (invariant culture): a stand-in identifier for rows without a name.</summary>
    public static string Coord(double ra, double dec) =>
        string.Create(CultureInfo.InvariantCulture, $"{ra:F4},{dec:F4}");

    /// <summary>A cross-matcher holding every row of <paramref name="t"/> that has a position.</summary>
    public static SkyCrossMatcher Index(TsvTable t, string raCol, string decCol, double toleranceArcsec)
    {
        var m = new SkyCrossMatcher(toleranceArcsec);
        for (int i = 0; i < t.Count; i++)
            if (Position(t, i, raCol, decCol) is { } p) m.Add(p.Ra, p.Dec, i);
        return m;
    }
}
