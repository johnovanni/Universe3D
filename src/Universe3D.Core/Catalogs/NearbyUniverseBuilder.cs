using System.Globalization;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using static Universe3D.Core.Catalogs.BuilderSupport;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Nearby universe: the 2MASS Redshift Survey, with HyperLEDA (diameter, PA, axis ratio) and the measured distances of
/// McConnachie, UNGC and Cosmicflows-4 (in that order, spec §3) cross-matched by position.
/// </summary>
public sealed class NearbyUniverseBuilder(DistanceResolver? resolver = null) : IDataSetBuilder
{
    private const string TwoMrsSource = "J/ApJS/199/26/table3";
    private const string Cf4Source = "J/ApJ/944/94/table2";
    private const string Cf4Name = PeculiarVelocityRule.Cosmicflows4;
    private const double MatchArcsec = 30;
    private const string UngcSource = "J/AJ/145/101/catalog";
    private const string MccSource = "J/AJ/144/4/catalog";
    private static readonly string[] UngcColumns =
        ["Name", "_RAJ2000", "_DEJ2000", "a26", "b/a", "TT", "Tdw", "HRV", "Dist", "i", "Vlg"];
    private static readonly string[] MccColumns =
        ["Name", "MType", "_RAJ2000", "_DEJ2000", "D", "HRV", "PA", "Ell"];

    /// <summary>
    /// A Local Volume galaxy matches a 2MRS galaxy within max(30″, 0.1 × its a26 diameter), at most 120″ (the
    /// catalogs centre large, irregular galaxies differently), and only if their velocities agree within 300 km/s.
    /// </summary>
    private const double LocalMaxArcsec = 120, LocalA26Fraction = 0.1, LocalMaxDeltaVKms = 300;

    // CAT holds the catalog designation ("MESSIER_087", "NGC_0253"); SimbadName is the 2MASX name.
    private static readonly string[] TwoMrsColumns =
        ["ID", "RAJ2000", "DEJ2000", "Kcmag", "b/a", "type", "cz", "Riso", "CAT", "SimbadName"];
    private static readonly string[] Cf4Columns = ["PGC", "RAJ2000", "DEJ2000", "DM", "Vcmb"];

    private readonly DistanceResolver _resolver = resolver ?? new DistanceResolver(CosmologyModel.Default);

    public DataSetKind Kind => DataSetKind.NearbyUniverse;

    public async Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Downloading the 2MASS Redshift Survey (J/ApJS/199/26)…");
        var twoMrs = await client.QueryAsync(TwoMrsSource, TwoMrsColumns, null, ct).ConfigureAwait(false);
        progress?.Report("Downloading HyperLEDA (VII/237)…");
        var leda = await client.QueryAsync(HyperLedaSource, HyperLedaColumns, HyperLedaConstraints, ct)
            .ConfigureAwait(false);
        progress?.Report("Downloading Cosmicflows-4 (J/ApJ/944/94)…");
        var cf4 = await client.QueryAsync(Cf4Source, Cf4Columns, null, ct).ConfigureAwait(false);
        progress?.Report("Downloading UNGC (J/AJ/145/101)…");
        var ungc = await client.QueryAsync(UngcSource, UngcColumns, null, ct).ConfigureAwait(false);
        progress?.Report("Downloading McConnachie 2012 (J/AJ/144/4)…");
        var mcc = await client.QueryAsync(MccSource, MccColumns, null, ct).ConfigureAwait(false);
        progress?.Report("Building the nearby universe…");

        var local = LocalVolumeBuilder.MeasuredDistances(mcc, ungc);
        var localFor = MatchLocalVolume(twoMrs, local);

        var ledaIndex = Index(leda, "_RAJ2000", "_DEJ2000", MatchArcsec);
        var cf4Index = Index(cf4, "RAJ2000", "DEJ2000", MatchArcsec);
        var cf4ByPgc = new Dictionary<int, int>();
        for (int i = 0; i < cf4.Count; i++)
            if (cf4.GetInt(i, "PGC") is { } pgc) cf4ByPgc.TryAdd(pgc, i);

        var objects = new List<CosmicObject>(twoMrs.Count);
        int skipped = 0, excluded = 0, cf4Used = 0, localUsed = 0;
        for (int i = 0; i < twoMrs.Count; i++)
        {
            if (i % 4096 == 0) ct.ThrowIfCancellationRequested();
            if (Position(twoMrs, i, "RAJ2000", "DEJ2000") is not { } pos) { skipped++; continue; }

            int l = ledaIndex.FindNearest(pos.Ra, pos.Dec);
            int? pgc = l >= 0 ? leda.GetInt(l, "PGC") : null;
            var lv = localFor[i] >= 0 ? local[localFor[i]] : null;
            double? dMpc = lv?.DistanceMpc;
            string? dCatalog = lv?.Catalog;
            if (dMpc == null)
            {
                int c = pgc is { } p && cf4ByPgc.TryGetValue(p, out int byPgc) ? byPgc : cf4Index.FindNearest(pos.Ra, pos.Dec);
                dMpc = c >= 0 && cf4.GetDouble(c, "DM") is { } dm ? Math.Pow(10, (dm - 25) / 5) : null;
                if (dMpc != null) dCatalog = Cf4Name;
            }
            double? cz = twoMrs.GetDouble(i, "cz");
            if (dMpc == null && cz == null) { skipped++; continue; }

            var (d25, ledaQ, ledaPa, ledaMorph) = Leda(leda, l);
            var morph = MorphologyParser.FromZcat(twoMrs.Get(i, "type"));
            if (morph.Class == MorphologyClass.Unknown && ledaMorph.Class != MorphologyClass.Unknown) morph = ledaMorph;
            // Riso is log10 of the isophotal radius in arcsec.
            double? isoArcmin = twoMrs.GetDouble(i, "Riso") is { } riso ? 2 * Math.Pow(10, riso) / 60.0 : null;

            var sources = new List<string> { "2MRS" };
            if (l >= 0) sources.Add("HyperLEDA");
            if (lv != null) sources.AddRange(lv.Sources);
            else if (dMpc != null) sources.Add(Cf4Name);
            var (pa, q) = Shape(ledaPa, ledaQ, lv?.MccPaDeg, lv?.MccAxisRatio, twoMrs.GetDouble(i, "b/a"),
                lv?.UngcAxisRatio);

            string id = twoMrs.Get(i, "ID") ?? Coord(pos.Ra, pos.Dec);
            var input = new GalaxyInput(
                Id: "2MRS:" + id,
                Designations: [Designation(twoMrs.Get(i, "CAT")),
                    pgc is { } pg ? string.Create(CultureInfo.InvariantCulture, $"PGC {pg}") : null,
                    twoMrs.Get(i, "SimbadName") ?? "2MASX J" + id],
                Ra: pos.Ra, Dec: pos.Dec,
                MeasuredMpc: dMpc, MeasuredCatalog: dCatalog,
                CzHelio: cz, Morphology: morph,
                PaDeg: pa,
                AxisRatio: q,
                InclinationDeg: lv?.UngcInclinationDeg,
                DiameterArcmin: lv?.A26Arcmin ?? d25 ?? isoArcmin,
                Magnitude: twoMrs.GetDouble(i, "Kcmag"),
                Sources: sources.ToArray());

            if (MakeGalaxy(input, _resolver) is { } g)
            {
                objects.Add(g);
                if (lv != null) localUsed++;
                else if (g.DistanceSource == DistanceSource.Measured) cf4Used++;
            }
            else excluded++;
        }

        string summary = $"2MRS ({TwoMrsSource}): {twoMrs.Count} rows; HyperLEDA ({HyperLedaSource}): {leda.Count} rows; " +
                         $"McConnachie 2012 ({MccSource}) and UNGC ({UngcSource}): {localUsed} measured distances used; " +
                         $"{Cf4Name} ({Cf4Source}): {cf4.Count} rows, {cf4Used} measured distances used";
        return new DataSet(Kind, objects, excluded, skipped, DateTime.UtcNow, summary);
    }

    /// <summary>
    /// For each 2MRS row, the index of the Local Volume entry that is the same galaxy, or -1. Candidate pairs lie
    /// within the entry's tolerance and agree in velocity when both have one; they are taken nearest first, each entry
    /// and each row at most once.
    /// </summary>
    private static int[] MatchLocalVolume(TsvTable twoMrs, IReadOnlyList<LocalVolumeEntry> local)
    {
        var index = Index(twoMrs, "RAJ2000", "DEJ2000", LocalMaxArcsec);
        var pairs = new List<(double Sep, int Row, int Entry)>();
        for (int e = 0; e < local.Count; e++)
        {
            var lv = local[e];
            double tol = Math.Clamp(LocalA26Fraction * (lv.A26Arcmin ?? 0) * 60, MatchArcsec, LocalMaxArcsec);
            foreach (var (row, sep) in index.FindWithin(lv.Ra, lv.Dec, tol))
            {
                if (lv.HrvKms is { } v && twoMrs.GetDouble(row, "cz") is { } cz && Math.Abs(cz - v) > LocalMaxDeltaVKms)
                    continue;
                pairs.Add((sep, row, e));
            }
        }

        var result = new int[twoMrs.Count];
        Array.Fill(result, -1);
        var taken = new bool[local.Count];
        foreach (var (_, row, e) in pairs.OrderBy(p => p.Sep).ThenBy(p => p.Row).ThenBy(p => p.Entry))
        {
            if (result[row] >= 0 || taken[e]) continue;
            result[row] = e;
            taken[e] = true;
        }
        return result;
    }

    /// <summary>
    /// The 2MRS CAT designation, or null for the catalog's internal ids ("g0912194-241021", "04161030-5546510")
    /// that are not names. Underscores in unrecognised designations become spaces.
    /// </summary>
    private static string? Designation(string? cat)
    {
        if (cat == null || cat.Length < 2 || !char.IsLetter(cat[0]) || !char.IsLetter(cat[1])) return null;
        string n = CommonNames.NormalizeDesignation(cat);
        return n == cat ? cat.Replace('_', ' ') : n;
    }
}
