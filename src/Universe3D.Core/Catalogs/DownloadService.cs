using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// "Download latest": rebuilds a data set from VizieR and saves it to the store. The store is written only after a
/// complete build, so a cancelled or failed download leaves the existing copy untouched.
/// </summary>
public sealed class DownloadService(IDataSetStore store, IVizierClient client,
                                    Func<DataSetKind, IDataSetBuilder>? builderFor = null)
{
    private readonly Func<DataSetKind, IDataSetBuilder> _builderFor = builderFor ?? DefaultBuilder;

    public static IDataSetBuilder DefaultBuilder(DataSetKind kind) => kind switch
    {
        DataSetKind.LocalVolume => new LocalVolumeBuilder(),
        DataSetKind.NearbyUniverse => new NearbyUniverseBuilder(),
        DataSetKind.Clusters => new ClusterBuilder(),
        // Download everything: the app's quasar limit setting decides how many are shown.
        DataSetKind.Quasars => new QuasarBuilder(int.MaxValue),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <exception cref="VizierUnavailableException">Every mirror failed; nothing was saved.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; nothing was saved.</exception>
    public async Task<DataSet> DownloadAsync(DataSetKind kind, IProgress<string>? progress, CancellationToken ct)
    {
        var dataSet = await _builderFor(kind).BuildAsync(client, progress, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (dataSet.Kind != kind)
            throw new InvalidOperationException($"The build produced {dataSet.Kind}, not {kind}.");
        if (dataSet.Objects.Count == 0)
            throw new InvalidDataException("The download produced no objects, so the existing data was kept.");
        progress?.Report("Saving…");
        store.Save(dataSet);
        return dataSet;
    }
}
