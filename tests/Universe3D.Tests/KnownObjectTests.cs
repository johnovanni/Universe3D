using Universe3D.Core.Catalogs;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

/// <summary>Checks the real built-in snapshots (no cache folder is consulted).</summary>
public class KnownObjectTests
{
    private static readonly string NoCache = Path.Combine(Path.GetTempPath(), "Universe3D.NoCache." + Guid.NewGuid().ToString("N"));

    private static readonly Dictionary<DataSetKind, DataSet> Cache = new();

    private static DataSet Load(DataSetKind kind)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(kind, out var ds))
            {
                ds = new SnapshotStore(NoCache).Load(kind, out bool fromCache);
                Assert.False(fromCache);
                Cache[kind] = ds;
            }
            return ds;
        }
    }

    public static IEnumerable<object[]> Kinds() =>
        Enum.GetValues<DataSetKind>().Select(k => new object[] { k });

    [Fact]
    public void LocalVolume_M31()
    {
        var ds = Load(DataSetKind.LocalVolume);
        var m31 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("M 31"));
        Assert.InRange(m31.DistanceMpc, 0.7, 0.8);
        Assert.InRange(m31.CzHelio!.Value, -310, -290);
        Assert.Equal(MorphologyClass.Spiral, m31.Morphology.Class);
        Assert.NotNull(m31.PaDeg);
        Assert.Equal(DistanceSource.Measured, m31.DistanceSource);
        Assert.Equal("Andromeda Galaxy", m31.Name);
    }

    [Fact]
    public void NearbyUniverse_M87()
    {
        var ds = Load(DataSetKind.NearbyUniverse);
        var m87 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("M 87"));
        Assert.InRange(m87.DistanceMpc, 15, 18);
        Assert.Equal(MorphologyClass.Elliptical, m87.Morphology.Class);
        Assert.Contains("Virgo A", m87.AltNames);
    }

    [Fact]
    public void Clusters_Coma()
    {
        var ds = Load(DataSetKind.Clusters);
        var coma = Assert.Single(ds.Objects.OfType<GalaxyCluster>(), c => c.Name.Contains("Coma Cluster"));
        Assert.Equal(0.0231, coma.Z!.Value, 0.002);
        Assert.Contains("Abell 1656", coma.AltNames);
    }

    [Fact]
    public void Quasars_3C273()
    {
        var ds = Load(DataSetKind.Quasars);
        var q = Assert.Single(ds.Objects.OfType<Quasar>(), o => o.Name == "3C 273");
        Assert.Equal(0.158, q.Z!.Value, 0.001);
        Assert.Equal(QuasarType.Quasar, q.Type);
    }

    [Fact]
    public void MilkyWay_CenterIs8p2Kpc()
    {
        var c = MilkyWayModel.CenterEq;
        Assert.Equal(0.0082, Math.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z), 1e-6);
        var (l, b) = SkyMath.ToSpherical(SkyFrames.EquatorialToGalactic * c);
        double dl = Math.Min(l, 360 - l);
        Assert.InRange(dl, 0, 0.01);
        Assert.InRange(Math.Abs(b), 0, 0.01);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AllObjects_HaveFinitePositions(DataSetKind kind)
    {
        var ds = Load(kind);
        Assert.NotEmpty(ds.Objects);
        foreach (var o in ds.Objects)
        {
            Assert.True(double.IsFinite(o.RaDeg) && double.IsFinite(o.DecDeg), o.Id);
            Assert.True(double.IsFinite(o.DistanceMpc) && o.DistanceMpc > 0, o.Id);
            var p = o.PositionEq;
            Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), o.Id);
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AllObjects_HaveUniqueIds(DataSetKind kind)
    {
        var ds = Load(kind);
        var dup = ds.Objects.GroupBy(o => o.Id).Where(g => g.Count() > 1).Select(g => g.Key).Take(5).ToList();
        Assert.True(dup.Count == 0, "Duplicate ids: " + string.Join(", ", dup));
    }
}
