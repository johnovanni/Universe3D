using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using static Universe3D.Core.Catalogs.BuilderSupport;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Local Group and Local Volume: UNGC (Karachentsev+ 2013) merged with McConnachie 2012, which wins for galaxies
/// matched by name or by position (60″, or a quarter of the larger diameter). HyperLEDA fills in PA, axis ratio, diameter and morphology where both lack them.
/// </summary>
public sealed class LocalVolumeBuilder(DistanceResolver? resolver = null) : IDataSetBuilder
{
    private const string UngcSource = "J/AJ/145/101/catalog";
    private const string MccSource = "J/AJ/144/4/catalog";
    private const string UngcName = "UNGC";
    private const string MccName = "McConnachie 2012";
    private const double MatchArcsec = 60;

    private static readonly string[] UngcColumns =
        ["Name", "_RAJ2000", "_DEJ2000", "a26", "b/a", "TT", "Tdw", "HRV", "Dist", "i", "Vlg"];
    private static readonly string[] MccColumns =
        ["Name", "MType", "_RAJ2000", "_DEJ2000", "D", "HRV", "PA", "Ell"];

    private readonly DistanceResolver _resolver = resolver ?? new DistanceResolver(CosmologyModel.Default);

    public DataSetKind Kind => DataSetKind.LocalVolume;

    public async Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Downloading UNGC (J/AJ/145/101)…");
        var ungc = await client.QueryAsync(UngcSource, UngcColumns, null, ct).ConfigureAwait(false);
        progress?.Report("Downloading McConnachie 2012 (J/AJ/144/4)…");
        var mcc = await client.QueryAsync(MccSource, MccColumns, null, ct).ConfigureAwait(false);
        progress?.Report("Downloading HyperLEDA (VII/237)…");
        var leda = await client.QueryAsync(HyperLedaSource, HyperLedaColumns, HyperLedaConstraints, ct)
            .ConfigureAwait(false);
        progress?.Report("Building the Local Volume…");

        var ledaIndex = Index(leda, "_RAJ2000", "_DEJ2000", MatchArcsec);
        var ungcFor = MatchToUngc(mcc, ungc);
        var ungcTaken = new bool[ungc.Count];
        var inputs = new List<GalaxyInput>();
        int skipped = 0, merged = 0;

        for (int i = 0; i < mcc.Count; i++)
        {
            if (Position(mcc, i, "_RAJ2000", "_DEJ2000") is not { } pos) { skipped++; continue; }
            int u = ungcFor[i];
            var input = FromMcConnachie(mcc, i, pos, ungc, u, leda, ledaIndex.FindNearest(pos.Ra, pos.Dec));
            if (input == null) { skipped++; continue; }
            if (u >= 0) { ungcTaken[u] = true; merged++; }
            inputs.Add(input);
        }

        for (int i = 0; i < ungc.Count; i++)
        {
            if (ungcTaken[i]) continue;
            if (Position(ungc, i, "_RAJ2000", "_DEJ2000") is not { } pos) { skipped++; continue; }
            var input = FromUngc(ungc, i, pos, leda, ledaIndex.FindNearest(pos.Ra, pos.Dec));
            if (input == null) skipped++; else inputs.Add(input);
        }

        var objects = new List<CosmicObject>(inputs.Count);
        int excluded = 0;
        foreach (var input in inputs)
        {
            if (MakeGalaxy(input, _resolver) is { } g) objects.Add(g);
            else excluded++;
        }

        string summary = $"{UngcName} ({UngcSource}): {ungc.Count} rows; {MccName} ({MccSource}): {mcc.Count} rows, " +
                         $"{merged} merged with UNGC; HyperLEDA ({HyperLedaSource}): {leda.Count} rows";
        return new DataSet(Kind, objects, excluded, skipped, DateTime.UtcNow, summary);
    }

    /// <summary>
    /// The UNGC row each McConnachie row is the same object as, or -1. Candidate pairs are name matches (best) or
    /// positions within max(60″, 0.25 × the UNGC a26 diameter); they are taken best-first, each UNGC row at most
    /// once, so a second-nearest candidate is used when the nearest is already taken.
    /// </summary>
    internal static int[] MatchToUngc(TsvTable mcc, TsvTable ungc)
    {
        var ungcKeys = new HashSet<string>[ungc.Count];
        for (int u = 0; u < ungc.Count; u++) ungcKeys[u] = [.. CommonNames.MatchKeys(ungc.Get(u, "Name"))];

        var pairs = new List<(int Rank, double Sep, int M, int U)>();
        for (int m = 0; m < mcc.Count; m++)
        {
            if (Position(mcc, m, "_RAJ2000", "_DEJ2000") is not { } mp) continue;
            var mKeys = CommonNames.MatchKeys(mcc.Get(m, "Name"));
            for (int u = 0; u < ungc.Count; u++)
            {
                double sep = double.PositiveInfinity;
                if (Position(ungc, u, "_RAJ2000", "_DEJ2000") is { } up)
                    sep = SkyMath.AngularSeparationDeg(mp.Ra, mp.Dec, up.Ra, up.Dec) * 3600;
                bool sameName = mKeys.Any(ungcKeys[u].Contains);
                double tol = Math.Max(MatchArcsec, 0.25 * (ungc.GetDouble(u, "a26") ?? 0) * 60);
                if (sameName) pairs.Add((0, double.IsInfinity(sep) ? 0 : sep, m, u));
                else if (sep <= tol) pairs.Add((1, sep, m, u));
            }
        }

        var result = new int[mcc.Count];
        Array.Fill(result, -1);
        var taken = new bool[ungc.Count];
        foreach (var (_, _, m, u) in pairs.OrderBy(p => p.Rank).ThenBy(p => p.Sep))
        {
            if (result[m] >= 0 || taken[u]) continue;
            result[m] = u;
            taken[u] = true;
        }
        return result;
    }

    /// <summary>
    /// A McConnachie galaxy; the matched UNGC row (<paramref name="u"/> &gt;= 0) fills its gaps, then HyperLEDA.
    /// Null when it has neither a distance nor a velocity.
    /// </summary>
    private static GalaxyInput? FromMcConnachie(TsvTable mcc, int i, (double Ra, double Dec) pos,
        TsvTable ungc, int u, TsvTable leda, int ledaRow)
    {
        double? U(string col) => u >= 0 ? ungc.GetDouble(u, col) : null;
        var (d25, ledaQ, ledaPa, ledaMorph) = Leda(leda, ledaRow);
        double? mccMpc = mcc.GetDouble(i, "D") / 1000.0;
        double? measured = mccMpc ?? U("Dist");
        double? cz = mcc.GetDouble(i, "HRV") ?? U("HRV");
        if (measured == null && cz == null) return null;

        var morph = MorphologyParser.FromMcConnachie(mcc.Get(i, "MType"));
        if (morph.Class == MorphologyClass.Unknown && u >= 0) morph = UngcMorphology(ungc, u);
        if (morph.Class == MorphologyClass.Unknown && ledaMorph.Class != MorphologyClass.Unknown) morph = ledaMorph;

        var sources = new List<string> { MccName };
        if (u >= 0) sources.Add(UngcName);
        if (ledaRow >= 0) sources.Add("HyperLEDA");

        string name = mcc.Get(i, "Name") ?? Coord(pos.Ra, pos.Dec);
        return new GalaxyInput(
            Id: "MCC:" + name,
            Designations: [name, u >= 0 ? ungc.Get(u, "Name") : null],
            Ra: pos.Ra, Dec: pos.Dec,
            MeasuredMpc: measured, MeasuredCatalog: mccMpc != null ? MccName : UngcName,
            CzHelio: cz, Morphology: morph,
            PaDeg: mcc.GetDouble(i, "PA") ?? ledaPa,
            AxisRatio: (1 - mcc.GetDouble(i, "Ell")) ?? U("b/a") ?? ledaQ,
            InclinationDeg: U("i"),
            DiameterArcmin: U("a26") ?? d25,
            Magnitude: null, Sources: sources.ToArray());
    }

    /// <summary>A UNGC galaxy not in McConnachie; null when it has neither a distance nor a velocity.</summary>
    private static GalaxyInput? FromUngc(TsvTable ungc, int i, (double Ra, double Dec) pos, TsvTable leda, int ledaRow)
    {
        var (d25, ledaQ, ledaPa, ledaMorph) = Leda(leda, ledaRow);
        double? measured = ungc.GetDouble(i, "Dist");
        double? cz = ungc.GetDouble(i, "HRV");
        if (measured == null && cz == null) return null;

        var morph = UngcMorphology(ungc, i);
        if (morph.Class == MorphologyClass.Unknown && ledaMorph.Class != MorphologyClass.Unknown) morph = ledaMorph;

        string name = ungc.Get(i, "Name") ?? Coord(pos.Ra, pos.Dec);
        return new GalaxyInput(
            Id: "UNGC:" + name, Designations: [name], Ra: pos.Ra, Dec: pos.Dec,
            MeasuredMpc: measured, MeasuredCatalog: UngcName, CzHelio: cz, Morphology: morph,
            PaDeg: ledaPa,
            AxisRatio: ungc.GetDouble(i, "b/a") ?? ledaQ,
            InclinationDeg: ungc.GetDouble(i, "i"),
            DiameterArcmin: ungc.GetDouble(i, "a26") ?? d25,
            Magnitude: null,
            Sources: ledaRow >= 0 ? [UngcName, "HyperLEDA"] : [UngcName]);
    }

    private static MorphologyInfo UngcMorphology(TsvTable ungc, int row) =>
        MorphologyParser.FromUngc(ungc.GetInt(row, "TT"), ungc.Get(row, "Tdw"));
}
