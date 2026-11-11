using System.IO.Compression;
using System.Text;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

public class SnapshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "u3d-test-" + Guid.NewGuid().ToString("N"));

    public SnapshotTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static DataSet Sample(DataSetKind kind = DataSetKind.LocalVolume)
    {
        var galaxy = new Galaxy
        {
            Id = "UNGC:M31", Name = "Andromeda", AltNames = new[] { "NGC 224", "M 31" },
            RaDeg = 10.6847, DecDeg = 41.2687, DistanceMpc = 0.77, DistanceSource = DistanceSource.Measured,
            DistanceCatalog = "UNGC", Z = -0.001, CzHelio = -300.0, VCmb = 12.5, PeculiarKms = -40.25,
            LookbackGyr = 0.0025, Magnitude = 3.44, Sources = new[] { "UNGC", "NED" },
            Morphology = new MorphologyInfo(MorphologyClass.Spiral, "Spiral galaxy", "SA(s)b", true),
            PaDeg = 35.0, AxisRatio = 0.4, InclinationDeg = 77.0, InclinationMeasured = true, DiameterKpc = 46.5,
        };
        var cluster = new GalaxyCluster
        {
            Id = "MCXC:J1230.7+1217", Name = "Virgo", AltNames = Array.Empty<string>(),
            RaDeg = 187.7, DecDeg = 12.39, DistanceMpc = 16.5, DistanceSource = DistanceSource.Redshift,
            DistanceCatalog = "MCXC", Z = null, CzHelio = null, VCmb = null, PeculiarKms = null,
            LookbackGyr = 0.2, Magnitude = null, Sources = new[] { "MCXC" },
            M500e14 = 4.2, R500Mpc = 1.1, L500e37W = null, Richness = 7, BautzMorgan = null,
            Catalogs = ClusterCatalogs.Mcxc | ClusterCatalogs.Abell,
        };
        var cluster2 = new GalaxyCluster
        {
            Id = "ABELL:1656", Name = "Coma", RaDeg = 194.95, DecDeg = 27.98, DistanceMpc = 100.0,
            Richness = null, BautzMorgan = "II", Catalogs = ClusterCatalogs.Abell,
        };
        var quasar = new Quasar
        {
            Id = "MQ:J1229+0203", Name = "3C 273", AltNames = new[] { "PG 1226+023" },
            RaDeg = 187.2779, DecDeg = 2.0525, DistanceMpc = 750.5, DistanceSource = DistanceSource.Redshift,
            DistanceCatalog = "MQ", Z = 0.158, CzHelio = 47400.0, VCmb = 47000.0, PeculiarKms = null,
            LookbackGyr = 2.4, Magnitude = 12.9, Sources = new[] { "MQ" },
            Type = QuasarType.BlLac, RMag = 12.8, BMag = null,
        };
        return new DataSet(kind, new CosmicObject[] { galaxy, cluster, cluster2, quasar }, 3, 5,
            new DateTime(2026, 11, 10, 12, 30, 15, DateTimeKind.Utc), "UNGC + test");
    }

    private static void AssertCommon(CosmicObject e, CosmicObject a)
    {
        Assert.Equal(e.GetType(), a.GetType());
        Assert.Equal(e.Id, a.Id);
        Assert.Equal(e.Name, a.Name);
        Assert.Equal(e.AltNames, a.AltNames);
        Assert.Equal(e.RaDeg, a.RaDeg);
        Assert.Equal(e.DecDeg, a.DecDeg);
        Assert.Equal(e.DistanceMpc, a.DistanceMpc);
        Assert.Equal(e.DistanceSource, a.DistanceSource);
        Assert.Equal(e.DistanceCatalog, a.DistanceCatalog);
        Assert.Equal(e.Z, a.Z);
        Assert.Equal(e.CzHelio, a.CzHelio);
        Assert.Equal(e.VCmb, a.VCmb);
        Assert.Equal(e.PeculiarKms, a.PeculiarKms);
        Assert.Equal(e.LookbackGyr, a.LookbackGyr);
        Assert.Equal(e.Magnitude, a.Magnitude);
        Assert.Equal(e.Sources, a.Sources);
        Assert.Equal(e.TypeCode, a.TypeCode);
        Assert.Equal(e.PositionEq, a.PositionEq);
    }

    private static void AssertSame(DataSet e, DataSet a)
    {
        Assert.Equal(e.Kind, a.Kind);
        Assert.Equal(e.ExcludedCount, a.ExcludedCount);
        Assert.Equal(e.SkippedRows, a.SkippedRows);
        Assert.Equal(e.BuiltUtc, a.BuiltUtc);
        Assert.Equal(e.BuiltUtc.Kind, a.BuiltUtc.Kind);
        Assert.Equal(e.SourceSummary, a.SourceSummary);
        Assert.Equal(e.DistanceRangeMpc, a.DistanceRangeMpc);
        Assert.Equal(e.Objects.Count, a.Objects.Count);
        for (int i = 0; i < e.Objects.Count; i++)
        {
            AssertCommon(e.Objects[i], a.Objects[i]);
            switch (e.Objects[i])
            {
                case Galaxy g:
                    var ag = (Galaxy)a.Objects[i];
                    Assert.Equal(g.Morphology, ag.Morphology);
                    Assert.Equal(g.PaDeg, ag.PaDeg);
                    Assert.Equal(g.AxisRatio, ag.AxisRatio);
                    Assert.Equal(g.InclinationDeg, ag.InclinationDeg);
                    Assert.Equal(g.InclinationMeasured, ag.InclinationMeasured);
                    Assert.Equal(g.DiameterKpc, ag.DiameterKpc);
                    break;
                case GalaxyCluster c:
                    var ac = (GalaxyCluster)a.Objects[i];
                    Assert.Equal(c.M500e14, ac.M500e14);
                    Assert.Equal(c.R500Mpc, ac.R500Mpc);
                    Assert.Equal(c.L500e37W, ac.L500e37W);
                    Assert.Equal(c.Richness, ac.Richness);
                    Assert.Equal(c.BautzMorgan, ac.BautzMorgan);
                    Assert.Equal(c.Catalogs, ac.Catalogs);
                    break;
                case Quasar q:
                    var aq = (Quasar)a.Objects[i];
                    Assert.Equal(q.Type, aq.Type);
                    Assert.Equal(q.RMag, aq.RMag);
                    Assert.Equal(q.BMag, aq.BMag);
                    break;
            }
        }
    }

    [Fact]
    public void Snapshot_RoundTripsEveryField()
    {
        var original = Sample();
        using var ms = new MemoryStream();
        SnapshotSerializer.Write(ms, original);
        ms.Position = 0;
        AssertSame(original, SnapshotSerializer.Read(ms));
    }

    [Fact]
    public void Snapshot_RejectsBadMagic()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(Encoding.ASCII.GetBytes("XXXX"));
            gz.Write(new byte[16]);
        }
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    [Fact]
    public void Snapshot_RejectsBadVersion()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(Encoding.ASCII.GetBytes("U3D1"));
            gz.Write(BitConverter.GetBytes(99));
            gz.Write(new byte[16]);
        }
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    [Fact]
    public void Snapshot_RejectsTruncatedData()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(Encoding.ASCII.GetBytes("U3D1"));
            gz.Write(BitConverter.GetBytes(1));
        }
        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    /// <summary>A gzip stream holding the snapshot header (magic, version) followed by <paramref name="body"/>.</summary>
    private static MemoryStream Gzipped(Action<BinaryWriter> body)
    {
        var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        using (var w = new BinaryWriter(gz, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Encoding.ASCII.GetBytes("U3D1"));
            w.Write(1);
            body(w);
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>A valid data-set header (Local Volume, one object) and the common fields of one galaxy.</summary>
    private static void HeaderAndGalaxyStart(BinaryWriter w, int kind = 0)
    {
        w.Write(kind); w.Write(0); w.Write(0); w.Write(0L); w.Write((int)DateTimeKind.Utc); w.Write("summary");
        w.Write(1);
        w.Write((byte)0); // galaxy
        w.Write("UNGC:X"); w.Write("X");
    }

    [Fact]
    public void Snapshot_RejectsHugeArrayLength()
    {
        // A garbage AltNames length must not be allocated (it would throw OutOfMemoryException).
        using var ms = Gzipped(w => { HeaderAndGalaxyStart(w); w.Write(int.MaxValue); });
        var ex = Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
        Assert.Contains("length", ex.Message);
    }

    [Fact]
    public void Snapshot_RejectsHugeObjectCount()
    {
        using var ms = Gzipped(w =>
        {
            w.Write(0); w.Write(0); w.Write(0); w.Write(0L); w.Write((int)DateTimeKind.Utc); w.Write("summary");
            w.Write(int.MaxValue);
        });
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    [Theory]
    [InlineData(99, 1)]    // data-set kind
    [InlineData(0, 99)]    // DateTimeKind
    public void Snapshot_RejectsUnknownHeaderEnums(int kind, int dateTimeKind)
    {
        using var ms = Gzipped(w =>
        {
            w.Write(kind); w.Write(0); w.Write(0); w.Write(0L); w.Write(dateTimeKind); w.Write("summary"); w.Write(0);
        });
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    [Theory]
    [InlineData(0)] // distance source
    [InlineData(1)] // morphology class
    [InlineData(2)] // quasar type
    [InlineData(3)] // cluster catalogs
    public void Snapshot_RejectsUnknownObjectEnums(int which)
    {
        // One object, written field by field as SnapshotSerializer does, with one bad enum value.
        using var ms = Gzipped(w =>
        {
            w.Write(0); w.Write(0); w.Write(0); w.Write(0L); w.Write((int)DateTimeKind.Utc); w.Write("summary");
            w.Write(1);
            byte tag = which switch { 2 => (byte)2, 3 => (byte)1, _ => (byte)0 };
            w.Write(tag); w.Write("ID:1"); w.Write("N"); w.Write(0);
            w.Write(1.0); w.Write(2.0); w.Write(3.0);
            w.Write(which == 0 ? 7 : 0); // distance source
            w.Write("cat");
            for (int i = 0; i < 4; i++) { w.Write(false); w.Write(0.0); } // z, cz, vcmb, pec
            w.Write(0.0); // lookback
            w.Write(false); w.Write(0.0); // magnitude
            w.Write(0); // sources
            switch (tag)
            {
                case 0:
                    w.Write(which == 1 ? 42 : 0); w.Write("d"); w.Write("c"); w.Write(false);
                    for (int i = 0; i < 3; i++) { w.Write(false); w.Write(0.0); }
                    w.Write(false);
                    w.Write(false); w.Write(0.0);
                    break;
                case 1:
                    for (int i = 0; i < 3; i++) { w.Write(false); w.Write(0.0); }
                    w.Write(false); w.Write(0); w.Write(false); w.Write("");
                    w.Write(8); // no such catalog bit
                    break;
                case 2:
                    w.Write(9); // no such quasar type
                    w.Write(false); w.Write(0.0); w.Write(false); w.Write(0.0);
                    break;
            }
        });
        Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(ms));
    }

    private (SnapshotStore store, string cacheDir) MakeStore()
    {
        string builtInPath = Path.Combine(_dir, "builtin.u3d.gz");
        using (var fs = File.Create(builtInPath)) SnapshotSerializer.Write(fs, Sample());
        string cache = Path.Combine(_dir, "cache");
        return (new SnapshotStore(cache, _ => File.OpenRead(builtInPath)), cache);
    }

    [Fact]
    public void Store_CorruptCacheFallsBackToBuiltIn()
    {
        var (store, cache) = MakeStore();
        Directory.CreateDirectory(cache);
        string file = Path.Combine(cache, "localvolume.u3d.gz");
        File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        var ds = store.Load(DataSetKind.LocalVolume, out bool fromCache, out string? problem);

        Assert.False(fromCache);
        Assert.False(File.Exists(file));
        Assert.Equal(4, ds.Objects.Count);
        Assert.NotNull(problem);
    }

    /// <summary>Ruling R40: valid gzip with a garbage payload (here a malformed string length) falls back too.</summary>
    [Theory]
    [InlineData(0)] // FormatException from a 7-bit length that never ends
    [InlineData(1)] // huge array length
    [InlineData(2)] // random bytes after the header
    public void Store_GarbagePayloadInCache_FallsBackDeletesAndReports(int variant)
    {
        var (store, cache) = MakeStore();
        Directory.CreateDirectory(cache);
        string file = Path.Combine(cache, "localvolume.u3d.gz");
        using (var payload = Gzipped(w =>
               {
                   switch (variant)
                   {
                       case 0:
                           w.Write(0); w.Write(0); w.Write(0); w.Write(0L); w.Write(1);
                           w.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                           break;
                       case 1:
                           HeaderAndGalaxyStart(w);
                           w.Write(int.MaxValue);
                           break;
                       default:
                           var rnd = new Random(1234);
                           var bytes = new byte[4096];
                           rnd.NextBytes(bytes);
                           w.Write(bytes);
                           break;
                   }
               }))
            File.WriteAllBytes(file, payload.ToArray());

        var ds = store.Load(DataSetKind.LocalVolume, out bool fromCache, out string? problem);

        Assert.False(fromCache);
        Assert.False(File.Exists(file));
        Assert.Equal(4, ds.Objects.Count);
        Assert.False(string.IsNullOrWhiteSpace(problem));
        Assert.Contains("deleted", problem);
    }

    [Fact]
    public void Store_WrongKindInCache_FallsBackAndReports()
    {
        var (store, cache) = MakeStore();
        Directory.CreateDirectory(cache);
        string file = Path.Combine(cache, "localvolume.u3d.gz");
        using (var fs = File.Create(file)) SnapshotSerializer.Write(fs, Sample(DataSetKind.Clusters));

        store.Load(DataSetKind.LocalVolume, out bool fromCache, out string? problem);

        Assert.False(fromCache);
        Assert.False(File.Exists(file));
        Assert.NotNull(problem);
    }

    [Fact]
    public void Store_GoodOrMissingCache_ReportsNoProblem()
    {
        var (store, _) = MakeStore();
        store.Load(DataSetKind.LocalVolume, out _, out string? none);
        Assert.Null(none);
        store.Save(Sample());
        store.Load(DataSetKind.LocalVolume, out bool fromCache, out string? stillNone);
        Assert.True(fromCache);
        Assert.Null(stillNone);
    }

    [Fact]
    public void Store_SaveThenLoadUsesCache()
    {
        var (store, _) = MakeStore();
        var saved = new DataSet(DataSetKind.LocalVolume, Sample().Objects.Take(1).ToList(), 0, 0,
            DateTime.UtcNow, "downloaded");
        store.Save(saved);

        Assert.True(store.HasDownloadedCopy(DataSetKind.LocalVolume));
        var ds = store.Load(DataSetKind.LocalVolume, out bool fromCache, out _);
        Assert.True(fromCache);
        Assert.Single(ds.Objects);
        Assert.Equal("downloaded", ds.SourceSummary);
    }

    [Fact]
    public void Store_ClearDownloaded_RevertsToBuiltIn()
    {
        var (store, _) = MakeStore();
        store.Save(new DataSet(DataSetKind.LocalVolume, Array.Empty<CosmicObject>(), 0, 0, DateTime.UtcNow, "x"));
        Assert.True(store.HasDownloadedCopy(DataSetKind.LocalVolume));

        store.ClearDownloaded(DataSetKind.LocalVolume);

        Assert.False(store.HasDownloadedCopy(DataSetKind.LocalVolume));
        var ds = store.Load(DataSetKind.LocalVolume, out bool fromCache, out _);
        Assert.False(fromCache);
        Assert.Equal(4, ds.Objects.Count);
    }

    [Fact]
    public void MilkyWay_CenterAndNormalPointTheRightWay()
    {
        var galactic = SkyFrames.EquatorialToGalactic;
        var center = galactic * MilkyWayModel.CenterEq;
        Assert.Equal(0.0082, center.X, 9);
        Assert.Equal(0.0, center.Y, 9);
        Assert.Equal(0.0, center.Z, 9);
        var normal = galactic * MilkyWayModel.NormalEq;
        Assert.Equal(1.0, normal.Z, 9);
        Assert.Equal(0.030, MilkyWayModel.DiameterMpc);
    }

    [Fact]
    public void CosmicObject_TypeCodes()
    {
        Assert.Equal((uint)MorphologyClass.Spiral,
            new Galaxy { Morphology = new MorphologyInfo(MorphologyClass.Spiral, "", "", false) }.TypeCode);
        Assert.Equal(2u, new GalaxyCluster { Catalogs = ClusterCatalogs.Abell }.TypeCode);
        Assert.Equal((uint)QuasarType.Type2, new Quasar { Type = QuasarType.Type2 }.TypeCode);
    }
}
