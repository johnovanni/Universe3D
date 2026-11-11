using System.Globalization;
using System.Text.RegularExpressions;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Objects;
using static Universe3D.Core.Catalogs.BuilderSupport;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Galaxy clusters: MCXC merged with the Abell (ACO) tables. An Abell cluster equals an MCXC cluster when the MCXC
/// alternative name carries its Abell number, or when they lie within 3′ with |Δz| &lt; 0.005. MCXC wins merges
/// (Id, position, z and X-ray values); the Abell name, richness and Bautz–Morgan type are kept.
/// </summary>
public sealed class ClusterBuilder(DistanceResolver? resolver = null) : IDataSetBuilder
{
    private const string McxcSource = "J/A+A/534/A109/mcxc";
    private const double MatchArcmin = 3;
    private const double MatchDz = 0.005;
    private const double PopularPositionArcmin = 30; // Ruling R3

    private static readonly string[] McxcColumns =
        ["MCXC", "OName", "AName", "_RAJ2000", "_DEJ2000", "z", "M500", "R500", "L500"];

    // Northern and southern ACO tables number clusters in "ACO"; the supplementary southern table in "ACOS".
    private static readonly (string Source, string NumberColumn, string Prefix)[] AbellTables =
    [
        ("VII/110A/table3", "ACO", ""),
        ("VII/110A/table4", "ACO", ""),
        ("VII/110A/table5", "ACOS", "S"),
    ];

    private static readonly IReadOnlyDictionary<string, string> RedshiftOnly =
        new Dictionary<string, string> { ["z"] = ">0" };

    // "A2717", "A0151N", "VMF98 7, A0122" -> 2717, 151, 122; "S0026", "S141a" -> S26, S141.
    private static readonly Regex AbellRef = new(@"(?<![A-Za-z0-9])(A|S)\s?0*(\d{1,4})(?!\d)",
        RegexOptions.CultureInvariant);

    private readonly DistanceResolver _resolver = resolver ?? new DistanceResolver(CosmologyModel.Default);

    public DataSetKind Kind => DataSetKind.Clusters;

    private sealed record Abell(string Key, double Ra, double Dec, double Z, int? Richness, string? BautzMorgan)
    {
        public string Designation => "Abell " + Key;
    }

    private sealed class Draft
    {
        public required string Id;
        public required double Ra, Dec, Z;
        public int McxcRow = -1;
        public List<Abell> Abells = new();
        public string? Popular;
    }

    public async Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Downloading MCXC (J/A+A/534/A109)…");
        var mcxc = await client.QueryAsync(McxcSource, McxcColumns, null, ct).ConfigureAwait(false);
        int skipped = 0;
        var abells = new List<Abell>();
        foreach (var (source, numberCol, prefix) in AbellTables)
        {
            progress?.Report($"Downloading Abell clusters ({source})…");
            var t = await client.QueryAsync(source, [numberCol, "_RAJ2000", "_DEJ2000", "z", "Rich", "BMtype"],
                RedshiftOnly, ct).ConfigureAwait(false);
            for (int i = 0; i < t.Count; i++)
            {
                if (Position(t, i, "_RAJ2000", "_DEJ2000") is not { } pos || t.GetDouble(i, "z") is not { } z
                    || t.GetInt(i, numberCol) is not { } number)
                {
                    skipped++;
                    continue;
                }
                abells.Add(new Abell(prefix + number.ToString(CultureInfo.InvariantCulture), pos.Ra, pos.Dec, z,
                    t.GetInt(i, "Rich"), t.Get(i, "BMtype")));
            }
        }
        progress?.Report("Building clusters…");

        // MCXC drafts.
        var drafts = new List<Draft>();
        for (int i = 0; i < mcxc.Count; i++)
        {
            if (Position(mcxc, i, "_RAJ2000", "_DEJ2000") is not { } pos || mcxc.GetDouble(i, "z") is not { } z
                || mcxc.Get(i, "MCXC") is not { } name)
            {
                skipped++;
                continue;
            }
            drafts.Add(new Draft { Id = "MCXC:" + name, Ra = pos.Ra, Dec = pos.Dec, Z = z, McxcRow = i });
        }

        // Merge pass 1: the MCXC names carry the Abell number.
        var byKey = new Dictionary<string, Abell>();
        foreach (var a in abells) byKey.TryAdd(a.Key, a);
        var taken = new HashSet<Abell>(ReferenceEqualityComparer.Instance);
        foreach (var d in drafts)
            foreach (var key in AbellKeys(mcxc.Get(d.McxcRow, "AName")).Concat(AbellKeys(mcxc.Get(d.McxcRow, "OName"))))
                if (byKey.TryGetValue(key, out var a) && taken.Add(a)) d.Abells.Add(a);

        // Merge pass 2: position and redshift; then the rest stand alone.
        int mcxcCount = drafts.Count;
        var bands = new Dictionary<int, List<Draft>>();
        foreach (var d in drafts) Band(bands, d.Dec).Add(d);
        foreach (var a in abells)
        {
            if (taken.Contains(a)) continue;
            if (NearestMcxc(bands, a) is { } d) d.Abells.Add(a);
            else drafts.Add(new Draft { Id = "ABELL:" + a.Key, Ra = a.Ra, Dec = a.Dec, Z = a.Z, Abells = { a } });
        }

        AssignPopularNames(drafts, mcxc);

        var objects = new List<CosmicObject>(drafts.Count);
        int excluded = 0, merged = 0;
        foreach (var d in drafts)
        {
            if (Make(d, mcxc) is { } c)
            {
                objects.Add(c);
                if (c.Catalogs == (ClusterCatalogs.Mcxc | ClusterCatalogs.Abell)) merged++;
            }
            else excluded++;
        }

        string summary = $"MCXC ({McxcSource}): {mcxc.Count} rows; Abell (VII/110A tables 3-5, z > 0): " +
                         $"{abells.Count} clusters; {merged} MCXC clusters merged with Abell";
        return new DataSet(Kind, objects, excluded, skipped, DateTime.UtcNow, summary);
    }

    private static IEnumerable<string> AbellKeys(string? text) =>
        text == null
            ? []
            : AbellRef.Matches(text).Select(m =>
                (m.Groups[1].Value == "S" ? "S" : "") + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));

    private static List<Draft> Band(Dictionary<int, List<Draft>> bands, double dec)
    {
        int b = (int)Math.Floor(dec);
        if (!bands.TryGetValue(b, out var list)) bands[b] = list = new();
        return list;
    }

    /// <summary>The nearest MCXC cluster within 3′ whose z differs by less than 0.005, or null.</summary>
    private static Draft? NearestMcxc(Dictionary<int, List<Draft>> bands, Abell a)
    {
        Draft? best = null;
        double bestSep = MatchArcmin / 60.0;
        int b0 = (int)Math.Floor(a.Dec);
        for (int b = b0 - 1; b <= b0 + 1; b++)
        {
            if (!bands.TryGetValue(b, out var list)) continue;
            foreach (var d in list)
            {
                if (Math.Abs(d.Z - a.Z) >= MatchDz) continue;
                double sep = SkyMath.AngularSeparationDeg(a.Ra, a.Dec, d.Ra, d.Dec);
                if (sep <= bestSep) { bestSep = sep; best = d; }
            }
        }
        return best;
    }

    /// <summary>
    /// Popular names: from an Abell designation, the MCXC OName or AName; then (Ruling R3) each of
    /// <see cref="CommonNames.ClusterPositions"/> not yet used goes to the nearest unnamed cluster within 30′.
    /// </summary>
    private static void AssignPopularNames(List<Draft> drafts, TsvTable mcxc)
    {
        foreach (var d in drafts)
        {
            IEnumerable<string?> candidates = d.Abells.Select(a => a.Designation);
            if (d.McxcRow >= 0)
                candidates = candidates.Append(mcxc.Get(d.McxcRow, "OName"))
                    .Concat((mcxc.Get(d.McxcRow, "AName") ?? "").Split(',').Select(s => s.Trim()));
            d.Popular = candidates
                .Select(n => n != null && CommonNames.Popular.TryGetValue(n, out var p) ? p : null)
                .FirstOrDefault(p => p != null);
        }

        var used = drafts.Select(d => d.Popular).Where(p => p != null).ToHashSet();
        foreach (var (name, ra, dec) in CommonNames.ClusterPositions)
        {
            if (used.Contains(name)) continue;
            var nearest = drafts.Where(d => d.Popular == null)
                .Select(d => (d, sep: SkyMath.AngularSeparationDeg(ra, dec, d.Ra, d.Dec)))
                .Where(x => x.sep <= PopularPositionArcmin / 60.0)
                .OrderBy(x => x.sep)
                .Select(x => x.d)
                .FirstOrDefault();
            if (nearest != null) nearest.Popular = name;
        }
    }

    private GalaxyCluster? Make(Draft d, TsvTable mcxc)
    {
        var res = _resolver.Resolve(d.Ra, d.Dec, null, null, null, d.Z);
        if (res == null) return null;

        bool hasMcxc = d.McxcRow >= 0;
        var primary = d.Abells.FirstOrDefault();
        var alt = new List<string>();
        void Add(string? n)
        {
            if (!string.IsNullOrWhiteSpace(n) && !alt.Contains(n)) alt.Add(n);
        }
        foreach (var a in d.Abells) Add(a.Designation);
        if (hasMcxc)
        {
            Add("MCXC " + mcxc.Get(d.McxcRow, "MCXC"));
            Add(mcxc.Get(d.McxcRow, "OName"));
            foreach (var part in (mcxc.Get(d.McxcRow, "AName") ?? "").Split(','))
                Add(CommonNames.NormalizeDesignation(part));
        }
        Add(d.Popular);

        string name = primary != null
            ? d.Popular != null ? $"{primary.Designation} ({d.Popular})" : primary.Designation
            : d.Popular ?? "MCXC " + mcxc.Get(d.McxcRow, "MCXC");

        return new GalaxyCluster
        {
            Id = d.Id, Name = name, AltNames = alt.ToArray(), RaDeg = d.Ra, DecDeg = d.Dec,
            DistanceMpc = res.DistanceMpc, DistanceSource = res.Source, DistanceCatalog = res.Catalog,
            Z = d.Z, CzHelio = d.Z * RedshiftMath.C, VCmb = res.VCmb, PeculiarKms = res.PeculiarKms,
            LookbackGyr = res.LookbackGyr,
            Sources = hasMcxc ? primary != null ? ["MCXC", "Abell"] : ["MCXC"] : ["Abell"],
            M500e14 = hasMcxc ? mcxc.GetDouble(d.McxcRow, "M500") : null,
            R500Mpc = hasMcxc ? mcxc.GetDouble(d.McxcRow, "R500") : null,
            L500e37W = hasMcxc ? mcxc.GetDouble(d.McxcRow, "L500") : null,
            Richness = primary?.Richness,
            BautzMorgan = primary?.BautzMorgan,
            Catalogs = (hasMcxc ? ClusterCatalogs.Mcxc : 0) | (primary != null ? ClusterCatalogs.Abell : 0),
        };
    }
}
