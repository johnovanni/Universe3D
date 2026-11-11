using System.Globalization;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using static Universe3D.Core.Catalogs.BuilderSupport;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Nearby universe: the 2MASS Redshift Survey, with HyperLEDA (diameter, PA, axis ratio) and Cosmicflows-4
/// (measured distances) cross-matched by position.
/// </summary>
public sealed class NearbyUniverseBuilder(DistanceResolver? resolver = null) : IDataSetBuilder
{
    private const string TwoMrsSource = "J/ApJS/199/26/table3";
    private const string Cf4Source = "J/ApJ/944/94/table2";
    private const string Cf4Name = PeculiarVelocityRule.Cosmicflows4;
    private const double MatchArcsec = 30;

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
        progress?.Report("Building the nearby universe…");

        var ledaIndex = Index(leda, "_RAJ2000", "_DEJ2000", MatchArcsec);
        var cf4Index = Index(cf4, "RAJ2000", "DEJ2000", MatchArcsec);
        var cf4ByPgc = new Dictionary<int, int>();
        for (int i = 0; i < cf4.Count; i++)
            if (cf4.GetInt(i, "PGC") is { } pgc) cf4ByPgc.TryAdd(pgc, i);

        var objects = new List<CosmicObject>(twoMrs.Count);
        int skipped = 0, excluded = 0, measured = 0;
        for (int i = 0; i < twoMrs.Count; i++)
        {
            if (i % 4096 == 0) ct.ThrowIfCancellationRequested();
            if (Position(twoMrs, i, "RAJ2000", "DEJ2000") is not { } pos) { skipped++; continue; }

            int l = ledaIndex.FindNearest(pos.Ra, pos.Dec);
            int? pgc = l >= 0 ? leda.GetInt(l, "PGC") : null;
            int c = pgc is { } p && cf4ByPgc.TryGetValue(p, out int byPgc) ? byPgc : cf4Index.FindNearest(pos.Ra, pos.Dec);
            double? dMpc = c >= 0 && cf4.GetDouble(c, "DM") is { } dm ? Math.Pow(10, (dm - 25) / 5) : null;
            double? cz = twoMrs.GetDouble(i, "cz");
            if (dMpc == null && cz == null) { skipped++; continue; }

            var (d25, ledaQ, ledaPa, ledaMorph) = Leda(leda, l);
            var morph = MorphologyParser.FromZcat(twoMrs.Get(i, "type"));
            if (morph.Class == MorphologyClass.Unknown && ledaMorph.Class != MorphologyClass.Unknown) morph = ledaMorph;
            // Riso is log10 of the isophotal radius in arcsec.
            double? isoArcmin = twoMrs.GetDouble(i, "Riso") is { } riso ? 2 * Math.Pow(10, riso) / 60.0 : null;

            var sources = new List<string> { "2MRS" };
            if (l >= 0) sources.Add("HyperLEDA");
            if (dMpc != null) sources.Add(Cf4Name);

            string id = twoMrs.Get(i, "ID") ?? Coord(pos.Ra, pos.Dec);
            var input = new GalaxyInput(
                Id: "2MRS:" + id,
                Designations: [Designation(twoMrs.Get(i, "CAT")),
                    pgc is { } pg ? string.Create(CultureInfo.InvariantCulture, $"PGC {pg}") : null,
                    twoMrs.Get(i, "SimbadName") ?? "2MASX J" + id],
                Ra: pos.Ra, Dec: pos.Dec,
                MeasuredMpc: dMpc, MeasuredCatalog: dMpc != null ? Cf4Name : null,
                CzHelio: cz, Morphology: morph,
                PaDeg: ledaPa,
                AxisRatio: twoMrs.GetDouble(i, "b/a") ?? ledaQ,
                InclinationDeg: null,
                DiameterArcmin: d25 ?? isoArcmin,
                Magnitude: twoMrs.GetDouble(i, "Kcmag"),
                Sources: sources.ToArray());

            if (MakeGalaxy(input, _resolver) is { } g)
            {
                objects.Add(g);
                if (g.DistanceSource == DistanceSource.Measured) measured++;
            }
            else excluded++;
        }

        string summary = $"2MRS ({TwoMrsSource}): {twoMrs.Count} rows; HyperLEDA ({HyperLedaSource}): {leda.Count} rows; " +
                         $"{Cf4Name} ({Cf4Source}): {cf4.Count} rows, {measured} measured distances used";
        return new DataSet(Kind, objects, excluded, skipped, DateTime.UtcNow, summary);
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
