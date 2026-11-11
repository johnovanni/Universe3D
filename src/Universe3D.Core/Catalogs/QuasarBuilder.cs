using Universe3D.Core.Cosmology;
using Universe3D.Core.Objects;
using static Universe3D.Core.Catalogs.BuilderSupport;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Quasars and AGN from the Million Quasars catalog (Milliquas). Rows are classified by the first letter of Type
/// (Q/L quasar, A AGN, B BL Lac, K/N type-2); any other type is dropped. The <paramref name="cap"/> brightest by
/// R magnitude, else B, are kept.
/// </summary>
public sealed class QuasarBuilder(int cap = 250_000, DistanceResolver? resolver = null) : IDataSetBuilder
{
    private const string Source = "VII/294/catalog";

    private static readonly string[] Columns = ["RAJ2000", "DEJ2000", "Name", "Type", "Rmag", "Bmag", "z"];

    private static readonly IReadOnlyDictionary<string, string> RedshiftOnly =
        new Dictionary<string, string> { ["z"] = ">0" };

    private readonly DistanceResolver _resolver = resolver ?? new DistanceResolver(CosmologyModel.Default);

    public DataSetKind Kind => DataSetKind.Quasars;

    public async Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Downloading Milliquas (VII/294)…");
        var t = await client.QueryAsync(Source, Columns, RedshiftOnly, ct).ConfigureAwait(false);
        progress?.Report("Building quasars…");

        var quasars = new List<Quasar>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int skipped = 0, excluded = 0, unclassified = 0;
        for (int i = 0; i < t.Count; i++)
        {
            if (i % 16384 == 0) ct.ThrowIfCancellationRequested();
            if (Position(t, i, "RAJ2000", "DEJ2000") is not { } pos || t.GetDouble(i, "z") is not { } z)
            {
                skipped++;
                continue;
            }
            if (Classify(t.Get(i, "Type")) is not { } type) { unclassified++; continue; }

            var res = _resolver.Resolve(pos.Ra, pos.Dec, null, null, null, z);
            if (res == null) { excluded++; continue; }

            string name = t.Get(i, "Name") ?? Coord(pos.Ra, pos.Dec);
            string id = "MQ:" + name;
            // Duplicate names get their position appended; the catalog's row order keeps this deterministic.
            if (!ids.Add(id))
            {
                string baseId = id + "@" + Coord(pos.Ra, pos.Dec);
                id = baseId;
                for (int n = 2; !ids.Add(id); n++) id = baseId + "#" + n;
            }

            double? r = t.GetDouble(i, "Rmag"), b = t.GetDouble(i, "Bmag");
            quasars.Add(new Quasar
            {
                Id = id, Name = name, RaDeg = pos.Ra, DecDeg = pos.Dec,
                DistanceMpc = res.DistanceMpc, DistanceSource = res.Source, DistanceCatalog = res.Catalog,
                Z = z, CzHelio = z * RedshiftMath.C, VCmb = res.VCmb, PeculiarKms = res.PeculiarKms,
                LookbackGyr = res.LookbackGyr, Magnitude = r ?? b, Sources = ["Milliquas"],
                Type = type, RMag = r, BMag = b,
            });
        }

        // OrderBy is stable, so equal magnitudes keep catalog order.
        var kept = quasars.OrderBy(q => q.Magnitude ?? double.PositiveInfinity).Take(Math.Max(0, cap))
            .ToList<CosmicObject>();
        string summary = $"Milliquas ({Source}, z > 0): {t.Count} rows, {unclassified} unclassified dropped; " +
                         $"{kept.Count} of {quasars.Count} kept (brightest by R, else B; limit {cap})";
        return new DataSet(Kind, kept, excluded, skipped, DateTime.UtcNow, summary);
    }

    /// <summary>The type from the first letter of the Milliquas Type code, or null when unclassified.</summary>
    internal static QuasarType? Classify(string? type) =>
        string.IsNullOrEmpty(type) ? null : type[0] switch
        {
            'Q' or 'L' => QuasarType.Quasar,
            'A' => QuasarType.Agn,
            'B' => QuasarType.BlLac,
            'K' or 'N' => QuasarType.Type2,
            _ => null,
        };
}
