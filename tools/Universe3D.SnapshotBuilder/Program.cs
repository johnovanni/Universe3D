using System.Diagnostics;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Objects;

// Usage: Universe3D.SnapshotBuilder <outDir> [--quasar-cap N] [localvolume|nearby|clusters|quasars ...]
const string Usage = "Usage: Universe3D.SnapshotBuilder <outDir> [--quasar-cap N] [localvolume|nearby|clusters|quasars ...]";

string? outDir = null;
int quasarCap = 250_000;
var kinds = new List<DataSetKind>();
for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    if (a == "--quasar-cap")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[++i], out quasarCap) || quasarCap <= 0)
        {
            Console.Error.WriteLine("--quasar-cap needs a positive integer.\n" + Usage);
            return 2;
        }
    }
    else if (ParseKind(a) is { } k) kinds.Add(k);
    else if (outDir == null && !a.StartsWith("--")) outDir = a;
    else
    {
        Console.Error.WriteLine($"Unrecognised argument '{a}'.\n{Usage}");
        return 2;
    }
}
if (outDir == null)
{
    Console.Error.WriteLine(Usage);
    return 2;
}
if (kinds.Count == 0) kinds.AddRange([DataSetKind.LocalVolume, DataSetKind.NearbyUniverse, DataSetKind.Clusters, DataSetKind.Quasars]);

Directory.CreateDirectory(outDir);
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
var client = new VizierClient(http);
var progress = new Progress<string>(m => Console.WriteLine("  " + m));
var store = new SnapshotStore(outDir);

foreach (var kind in kinds.Distinct())
{
    IDataSetBuilder builder = kind switch
    {
        DataSetKind.LocalVolume => new LocalVolumeBuilder(),
        DataSetKind.NearbyUniverse => new NearbyUniverseBuilder(),
        DataSetKind.Clusters => new ClusterBuilder(),
        _ => new QuasarBuilder(quasarCap),
    };
    Console.WriteLine($"Building {kind}…");
    var sw = Stopwatch.StartNew();
    var ds = await builder.BuildAsync(client, progress, CancellationToken.None);
    store.Save(ds);
    long bytes = new FileInfo(Path.Combine(outDir, SnapshotStore.FileName(kind))).Length;
    Console.WriteLine($"{kind}: {ds.Objects.Count} objects, {ds.SkippedRows} skipped, {ds.ExcludedCount} excluded, " +
                      $"{bytes / 1024.0 / 1024.0:F2} MB, {sw.Elapsed.TotalSeconds:F1} s");
}
return 0;

static DataSetKind? ParseKind(string s) => s.ToLowerInvariant() switch
{
    "localvolume" => DataSetKind.LocalVolume,
    "nearby" => DataSetKind.NearbyUniverse,
    "clusters" => DataSetKind.Clusters,
    "quasars" => DataSetKind.Quasars,
    _ => null,
};
