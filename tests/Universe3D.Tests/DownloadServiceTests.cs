using Universe3D.Core.Catalogs;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

public class DownloadServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "u3d-dl-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>A store whose built-in snapshot is a tiny local-volume data set, so Load works without a download.</summary>
    private SnapshotStore NewStore(DataSet builtIn) =>
        new(_dir, _ =>
        {
            var ms = new MemoryStream();
            SnapshotSerializer.Write(ms, builtIn);
            ms.Position = 0;
            return ms;
        });

    private static DataSet Tiny(DataSetKind kind) =>
        new(kind, Array.Empty<CosmicObject>(), 0, 0, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), "test");

    /// <summary>Cancels the token and throws when the first query arrives (the build is then under way).</summary>
    private sealed class CancellingClient(CancellationTokenSource cts) : IVizierClient
    {
        public Task<TsvTable> QueryAsync(string source, IReadOnlyList<string> columns,
            IReadOnlyDictionary<string, string>? constraints, CancellationToken ct)
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("not reached");
        }
    }

    private sealed class DownClient : IVizierClient
    {
        public Task<TsvTable> QueryAsync(string source, IReadOnlyList<string> columns,
            IReadOnlyDictionary<string, string>? constraints, CancellationToken ct) =>
            throw new VizierUnavailableException("https://a: HTTP 503; https://b: timed out");
    }

    private sealed class MemoryStore : IDataSetStore
    {
        public List<DataSet> Saved { get; } = new();
        public DataSet Load(DataSetKind kind, out bool fromCache) { fromCache = false; return Tiny(kind); }
        public void Save(DataSet dataSet) => Saved.Add(dataSet);
        public bool HasDownloadedCopy(DataSetKind kind) => Saved.Any(d => d.Kind == kind);
        public void ClearDownloaded(DataSetKind kind) => Saved.RemoveAll(d => d.Kind == kind);
    }

    [Fact]
    public async Task DownloadService_SavesOnSuccess()
    {
        var store = NewStore(Tiny(DataSetKind.LocalVolume));
        var service = new DownloadService(store, new FixtureVizierClient());
        var ds = await service.DownloadAsync(DataSetKind.LocalVolume, new Progress<string>(), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_dir, SnapshotStore.FileName(DataSetKind.LocalVolume))));
        var loaded = store.Load(DataSetKind.LocalVolume, out bool fromCache);
        Assert.True(fromCache);
        Assert.Equal(ds.Objects.Count, loaded.Objects.Count);
        Assert.NotEmpty(loaded.Objects);
    }

    [Fact]
    public async Task DownloadService_CancelLeavesSnapshotUnchanged()
    {
        var store = NewStore(Tiny(DataSetKind.LocalVolume));
        store.Save(Tiny(DataSetKind.LocalVolume));
        string file = Path.Combine(_dir, SnapshotStore.FileName(DataSetKind.LocalVolume));
        byte[] before = File.ReadAllBytes(file);

        using var cts = new CancellationTokenSource();
        var service = new DownloadService(store, new CancellingClient(cts));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadAsync(DataSetKind.LocalVolume, new Progress<string>(), cts.Token));

        Assert.Equal(before, File.ReadAllBytes(file));
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public async Task DownloadService_AllMirrorsDown_KeepsExisting()
    {
        var store = NewStore(Tiny(DataSetKind.Clusters));
        store.Save(Tiny(DataSetKind.Clusters));
        string file = Path.Combine(_dir, SnapshotStore.FileName(DataSetKind.Clusters));
        byte[] before = File.ReadAllBytes(file);

        var service = new DownloadService(store, new DownClient());
        var ex = await Assert.ThrowsAsync<VizierUnavailableException>(() =>
            service.DownloadAsync(DataSetKind.Clusters, new Progress<string>(), CancellationToken.None));

        Assert.Contains("HTTP 503", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(file));
    }

    [Fact]
    public async Task DownloadService_WorksWithAnyStore()
    {
        var store = new MemoryStore();
        var service = new DownloadService(store, new FixtureVizierClient());
        await service.DownloadAsync(DataSetKind.LocalVolume, new Progress<string>(), CancellationToken.None);

        var saved = Assert.Single(store.Saved);
        Assert.Equal(DataSetKind.LocalVolume, saved.Kind);
        Assert.NotEmpty(saved.Objects);
    }

    [Fact]
    public async Task DownloadService_EmptyResult_IsNotSaved()
    {
        var store = new MemoryStore();
        var service = new DownloadService(store, new FixtureVizierClient(), kind => new EmptyBuilder(kind));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(DataSetKind.Clusters, new Progress<string>(), CancellationToken.None));
        Assert.Empty(store.Saved);
    }

    private sealed class EmptyBuilder(DataSetKind kind) : IDataSetBuilder
    {
        public DataSetKind Kind => kind;
        public Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct) =>
            Task.FromResult(Tiny(kind));
    }
}

public class GpuDefaultsTests
{
    [Fact]
    public void Minimum_ReducesQuasarLimitAndShapeCap()
    {
        var d = GpuDefaults.For(GpuClass.Minimum);
        Assert.Equal(100_000, d.QuasarLimit);
        Assert.Equal(5_000, d.ShapeCap);
        Assert.True(d.Reduced);
    }

    [Fact]
    public void Recommended_KeepsFullDefaults()
    {
        var d = GpuDefaults.For(GpuClass.Recommended);
        Assert.Equal(250_000, d.QuasarLimit);
        Assert.Equal(20_000, d.ShapeCap);
        Assert.False(d.Reduced);
    }
}

public class GlFailureMessageTests
{
    [Theory]
    [InlineData("4.1.0 NVIDIA 551.23", true)]
    [InlineData("4.6.0 - Build 31.0.101.2111", true)]
    [InlineData("4.0.0", false)]
    [InlineData("3.3.0 Mesa", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("garbage", false)]
    public void IsSupported(string? version, bool expected) =>
        Assert.Equal(expected, GlFailureMessage.IsSupported(version));

    [Fact]
    public void Build_NamesRequirementRendererAndUsbHint()
    {
        string m = GlFailureMessage.Build("DisplayLink Virtual GPU", "3.3.0", null);
        Assert.Contains("OpenGL 4.1", m);
        Assert.Contains("DisplayLink Virtual GPU", m);
        Assert.Contains("3.3.0", m);
        Assert.Contains("USB display adapter", m);
        Assert.Contains("driven directly by the GPU", m);
    }

    [Fact]
    public void Build_WithoutContext_SaysRendererUnknownAndKeepsReason()
    {
        string m = GlFailureMessage.Build(null, null, "Failed to create context");
        Assert.Contains("OpenGL 4.1", m);
        Assert.Contains("not detected", m);
        Assert.Contains("Failed to create context", m);
        Assert.Contains("driven directly by the GPU", m);
    }
}
