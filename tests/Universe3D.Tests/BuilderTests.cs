using System.Text.RegularExpressions;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

/// <summary>Serves the captured VizieR fixtures by source, and checks every requested column exists in the file.</summary>
internal sealed class FixtureVizierClient : IVizierClient
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["J/AJ/145/101/catalog"] = "ungc.tsv",
        ["J/AJ/144/4/catalog"] = "mcconnachie.tsv",
        ["VII/237/pgc"] = "hyperleda.tsv",
        ["J/ApJS/199/26/table3"] = "twomrs.tsv",
        ["J/ApJ/944/94/table2"] = "cf4.tsv",
        ["J/A+A/534/A109/mcxc"] = "mcxc.tsv",
        ["VII/110A/table3"] = "abell3.tsv",
        ["VII/110A/table4"] = "abell4.tsv",
        ["VII/110A/table5"] = "abell5.tsv",
        ["VII/294/catalog"] = "milliquas.tsv",
    };

    public List<string> Sources { get; } = new();

    public static string PathOf(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

    public Task<TsvTable> QueryAsync(string source, IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string>? constraints, CancellationToken ct)
    {
        if (!Files.TryGetValue(source, out var file))
            throw new InvalidOperationException($"No fixture for source {source}");
        Sources.Add(source);
        string text = File.ReadAllText(PathOf(file));
        var header = text.Split('\n').First(l => l.Trim().Length > 0 && !l.StartsWith('#'))
            .Split('\t').Select(c => c.Trim()).ToHashSet();
        var missing = columns.Where(c => !header.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"{file} lacks requested columns: {string.Join(", ", missing)}");
        return Task.FromResult(TsvTable.Parse(new StringReader(text)));
    }
}

public class BuilderTests
{
    private static Task<DataSet> Build(IDataSetBuilder builder) =>
        builder.BuildAsync(new FixtureVizierClient(), null, CancellationToken.None);

    private static double QuasarMag(CosmicObject o) => o.Magnitude ?? double.PositiveInfinity;

    /// <summary>(Name, Type) of every data row in the Milliquas fixture.</summary>
    private static List<(string Name, string Type)> MilliquasRows()
    {
        var t = TsvTable.Parse(new StringReader(File.ReadAllText(FixtureVizierClient.PathOf("milliquas.tsv"))));
        return Enumerable.Range(0, t.Count).Select(i => (t.Get(i, "Name") ?? "", t.Get(i, "Type") ?? "")).ToList();
    }

    [Fact]
    public async Task LocalVolume_M31()
    {
        var ds = await Build(new LocalVolumeBuilder());
        Assert.Equal(DataSetKind.LocalVolume, ds.Kind);
        var m31 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("M 31"));
        Assert.InRange(m31.DistanceMpc, 0.7, 0.8);
        Assert.InRange(m31.CzHelio!.Value, -310, -290);
        Assert.Equal(MorphologyClass.Spiral, m31.Morphology.Class);
        Assert.NotNull(m31.PaDeg);
        Assert.Equal(DistanceSource.Measured, m31.DistanceSource);
        Assert.Equal("McConnachie 2012", m31.DistanceCatalog);
        Assert.Equal("Andromeda Galaxy", m31.Name);
        Assert.Contains("NGC 224", m31.AltNames);
    }

    [Theory]
    [InlineData("SMC", "MCC:SMC")]
    [InlineData("LMC", "MCC:LMC")]
    [InlineData("M 31", "MCC:Andromeda")]
    [InlineData("Sagittarius dSph", "MCC:Sagittarius dSph")]
    public async Task LocalVolume_LargeGalaxiesMergeOnce(string designation, string expectedId)
    {
        var ds = await Build(new LocalVolumeBuilder());
        var matches = ds.Objects.OfType<Galaxy>()
            .Where(g => g.Name == designation || g.AltNames.Contains(designation)
                        || (designation == "Sagittarius dSph" && g.Id.EndsWith("Sag dSph", StringComparison.Ordinal)))
            .ToList();
        var g = Assert.Single(matches);
        Assert.Equal(expectedId, g.Id);
        Assert.Contains("UNGC", g.Sources);
        Assert.DoesNotContain(ds.Objects, o => o.Id is "UNGC:SMC" or "UNGC:LMC" or "UNGC:MESSIER031" or "UNGC:Sag dSph");
    }

    [Fact]
    public async Task LocalVolume_NoDuplicateNamesOrPositions()
    {
        var ds = await Build(new LocalVolumeBuilder());
        var gals = ds.Objects.OfType<Galaxy>().ToList();
        Assert.Empty(gals.GroupBy(g => CommonNames.MatchKeys(g.Name).FirstOrDefault() ?? g.Name).Where(x => x.Count() > 1)
            .Select(x => x.Key));
        for (int i = 0; i < gals.Count; i++)
            for (int j = i + 1; j < gals.Count; j++)
                Assert.True(
                    Universe3D.Core.Coordinates.SkyMath.AngularSeparationDeg(gals[i].RaDeg, gals[i].DecDeg, gals[j].RaDeg, gals[j].DecDeg) * 3600 > 30,
                    $"{gals[i].Id} and {gals[j].Id} are within 30 arcsec");
    }

    [Fact]
    public async Task LocalVolume_UngcOnlyGalaxy_UsesUngcDistance()
    {
        var ds = await Build(new LocalVolumeBuilder());
        var n253 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("NGC 253"));
        Assert.Equal("UNGC:NGC0253", n253.Id);
        Assert.Equal("UNGC", n253.DistanceCatalog);
        Assert.Equal(3.94, n253.DistanceMpc, 6);
        Assert.Equal(85, n253.InclinationDeg);
        Assert.True(n253.InclinationMeasured);
        Assert.NotNull(n253.PaDeg); // from HyperLEDA, which UNGC lacks
        Assert.NotNull(n253.DiameterKpc);
    }

    [Fact]
    public async Task LocalVolume_DiameterUsesAngularDiameterDistance()
    {
        var ds = await Build(new LocalVolumeBuilder());
        var n253 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("NGC 253"));
        double z = Universe3D.Core.Cosmology.CosmologyModel.Default.RedshiftAtComovingDistance(3.94);
        double expected = 3940.0 * 37.15 / 60.0 * Math.PI / 180.0 / (1 + z);
        Assert.Equal(expected, n253.DiameterKpc!.Value, 6);
        Assert.True(z > 0 && expected < 3940.0 * 37.15 / 60.0 * Math.PI / 180.0); // D_A < D_C
    }

    [Fact]
    public async Task NearbyUniverse_M87()
    {
        var ds = await Build(new NearbyUniverseBuilder());
        Assert.Equal(DataSetKind.NearbyUniverse, ds.Kind);
        var m87 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("M 87"));
        Assert.InRange(m87.DistanceMpc, 15, 18);
        Assert.Equal(DistanceSource.Measured, m87.DistanceSource);
        Assert.Equal("Cosmicflows-4", m87.DistanceCatalog);
        Assert.Equal(MorphologyClass.Elliptical, m87.Morphology.Class);
        Assert.Contains("Virgo A", m87.AltNames);
        Assert.Equal("2MRS:12304942+1223279", m87.Id);
        Assert.Equal(1307, m87.CzHelio);
    }

    /// <summary>Ruling R39: McConnachie, then UNGC, then Cosmicflows-4 (spec §3), cross-matched by position.</summary>
    [Theory]
    [InlineData("M 31", "McConnachie 2012", 0.783)]
    [InlineData("M 32", "McConnachie 2012", 0.805)]
    [InlineData("NGC 253", "UNGC", 3.94)]
    [InlineData("M 81", "UNGC", 3.63)]
    public async Task NearbyUniverse_PrefersLocalVolumeDistances(string designation, string catalog, double mpc)
    {
        var ds = await Build(new NearbyUniverseBuilder());
        var g = Assert.Single(ds.Objects.OfType<Galaxy>(), o => o.AltNames.Contains(designation));
        Assert.Equal(DistanceSource.Measured, g.DistanceSource);
        Assert.Equal(catalog, g.DistanceCatalog);
        Assert.Equal(mpc, g.DistanceMpc, 6);
        Assert.Contains(catalog, g.Sources);
        Assert.StartsWith("2MRS:", g.Id);
    }

    [Fact]
    public async Task NearbyUniverse_UngcMatch_LendsInclinationAndDiameter()
    {
        var ds = await Build(new NearbyUniverseBuilder());
        var n253 = Assert.Single(ds.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("NGC 253"));
        Assert.Equal(85, n253.InclinationDeg);
        Assert.True(n253.InclinationMeasured);
        Assert.Equal(BuilderSupportDiameter(37.15, 3.94), n253.DiameterKpc!.Value, 6); // UNGC a26 first
    }

    private static double BuilderSupportDiameter(double arcmin, double mpc)
    {
        double z = Universe3D.Core.Cosmology.CosmologyModel.Default.RedshiftAtComovingDistance(mpc);
        return mpc * 1000.0 * arcmin / 60.0 * Math.PI / 180.0 / (1 + z);
    }

    /// <summary>Spec §3: PA and axis ratio come from HyperLEDA first, as a pair, before the catalog's own b/a.</summary>
    [Fact]
    public async Task Shape_PrefersHyperLedaPair()
    {
        double ledaQ = Math.Pow(10, -0.69); // HyperLEDA NGC 253: logR25 0.69, PA 53
        var nearby = await Build(new NearbyUniverseBuilder());
        var n = Assert.Single(nearby.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("NGC 253"));
        Assert.Equal(53, n.PaDeg);
        Assert.Equal(ledaQ, n.AxisRatio!.Value, 9); // not 2MRS b/a 0.264

        var lv = await Build(new LocalVolumeBuilder());
        var u = Assert.Single(lv.Objects.OfType<Galaxy>(), g => g.AltNames.Contains("NGC 253"));
        Assert.Equal(53, u.PaDeg);
        Assert.Equal(ledaQ, u.AxisRatio!.Value, 9); // not UNGC b/a 0.22
    }

    [Fact]
    public async Task NearbyUniverse_WithoutMeasuredDistance_UsesRedshift()
    {
        var ds = await Build(new NearbyUniverseBuilder());
        Assert.Contains(ds.Objects, o => o.DistanceSource == DistanceSource.Redshift);
        Assert.All(ds.Objects, o => Assert.True(o.DistanceMpc > 0));
    }

    [Fact]
    public async Task Clusters_Coma()
    {
        var ds = await Build(new ClusterBuilder());
        Assert.Equal(DataSetKind.Clusters, ds.Kind);
        var coma = Assert.Single(ds.Objects.OfType<GalaxyCluster>(), c => c.Name.Contains("Coma Cluster"));
        Assert.Equal(0.0231, coma.Z!.Value, 0.002);
        Assert.NotNull(coma.M500e14);
        Assert.Equal(ClusterCatalogs.Mcxc | ClusterCatalogs.Abell, coma.Catalogs);
        Assert.Equal("MCXC:J1259.7+2756", coma.Id);
        Assert.Equal(2, coma.Richness);
        Assert.Contains("Abell 1656", coma.AltNames);
        Assert.DoesNotContain(ds.Objects, o => o.Id == "ABELL:1656");
    }

    [Fact]
    public async Task Clusters_MergeByAbellNumberInAName()
    {
        var ds = await Build(new ClusterBuilder());
        // MCXC J0003.2-3555 has AName "A2717"; ACO 2717 is in the southern table.
        var c = Assert.Single(ds.Objects.OfType<GalaxyCluster>(), o => o.Id == "MCXC:J0003.2-3555");
        Assert.Equal(ClusterCatalogs.Mcxc | ClusterCatalogs.Abell, c.Catalogs);
        Assert.StartsWith("Abell 2717", c.Name);
        Assert.DoesNotContain(ds.Objects, o => o.Id == "ABELL:2717");
        Assert.Contains(ds.Objects.OfType<GalaxyCluster>(), o => o.Catalogs == ClusterCatalogs.Abell);
        Assert.Contains(ds.Objects.OfType<GalaxyCluster>(), o => o.Catalogs == ClusterCatalogs.Mcxc);
    }

    [Fact]
    public async Task Clusters_VirgoGetsPopularName()
    {
        var ds = await Build(new ClusterBuilder());
        var virgo = Assert.Single(ds.Objects, o => o.Id == "MCXC:J1230.7+1220");
        Assert.Equal("Virgo Cluster", virgo.Name);
    }

    [Fact]
    public async Task Quasars_3C273()
    {
        var ds = await Build(new QuasarBuilder());
        Assert.Equal(DataSetKind.Quasars, ds.Kind);
        var q = Assert.Single(ds.Objects.OfType<Quasar>(), o => o.Name == "3C 273");
        Assert.Equal(0.158, q.Z!.Value, 0.001);
        Assert.Equal(QuasarType.Quasar, q.Type);
        Assert.Equal("MQ:3C 273", q.Id);
    }

    /// <summary>
    /// Ruling R43: Milliquas gives z to 3 decimals, so z &lt; 0.005 would put nearby AGN such as NGC 253 inside the
    /// Local Group. Those galaxies are in the galaxy data sets.
    /// </summary>
    [Fact]
    public async Task Quasars_ExcludeVeryLowRedshift()
    {
        Assert.Contains(MilliquasRows(), r => r.Name == "NGC 253");
        var ds = await Build(new QuasarBuilder());
        Assert.DoesNotContain(ds.Objects, o => o.Name == "NGC 253");
        Assert.All(ds.Objects, o => Assert.True(o.Z >= QuasarBuilder.MinRedshift, o.Name));
        Assert.Equal(0.005, QuasarBuilder.MinRedshift);
        Assert.Contains("z < 0.005", ds.SourceSummary);
    }

    [Fact]
    public async Task Quasars_CapAndOrder()
    {
        var all = await Build(new QuasarBuilder());
        var capped = await Build(new QuasarBuilder(cap: 10));
        Assert.Equal(10, capped.Objects.Count);
        var mags = capped.Objects.Select(QuasarMag).ToList();
        Assert.Equal(mags.OrderBy(m => m), mags);
        Assert.Equal(all.Objects.Select(QuasarMag).OrderBy(m => m).Take(10), mags);
        Assert.True(all.Objects.Count > 10);
    }

    [Fact]
    public async Task Quasars_DropsUnclassified()
    {
        var rows = MilliquasRows();
        var unclassified = rows.Where(r => r.Type.Length == 0 || !"QLABKN".Contains(r.Type[0])).ToList();
        Assert.Contains(unclassified, r => r.Type == "R");
        Assert.Contains(unclassified, r => r.Type == "X");

        var ds = await Build(new QuasarBuilder());
        var names = ds.Objects.Select(o => o.Name).ToHashSet();
        Assert.All(unclassified, r => Assert.DoesNotContain(r.Name, names));
    }

    [Fact]
    public async Task Quasars_TypeFollowsFirstLetter()
    {
        var types = MilliquasRows().Where(r => r.Name.Length > 0).GroupBy(r => r.Name)
            .ToDictionary(g => g.Key, g => g.First().Type);
        var ds = await Build(new QuasarBuilder());
        foreach (var q in ds.Objects.OfType<Quasar>())
        {
            var expected = types[q.Name][0] switch
            {
                'Q' or 'L' => QuasarType.Quasar,
                'A' => QuasarType.Agn,
                'B' => QuasarType.BlLac,
                _ => QuasarType.Type2,
            };
            Assert.Equal(expected, q.Type);
        }
        Assert.Contains(ds.Objects.OfType<Quasar>(), q => q.Type == QuasarType.Agn);
        Assert.Contains(ds.Objects.OfType<Quasar>(), q => q.Type == QuasarType.Type2);
    }

    [Fact]
    public async Task Builder_CountsSkippedRows()
    {
        var ds = await Build(new QuasarBuilder());
        Assert.True(ds.SkippedRows >= 1);
        Assert.DoesNotContain(ds.Objects, o => o.Name.StartsWith("SYNTHETIC"));
    }

    private static IDataSetBuilder Make(DataSetKind kind) => kind switch
    {
        DataSetKind.LocalVolume => new LocalVolumeBuilder(),
        DataSetKind.NearbyUniverse => new NearbyUniverseBuilder(),
        DataSetKind.Clusters => new ClusterBuilder(),
        _ => new QuasarBuilder(),
    };

    [Theory]
    [InlineData(DataSetKind.LocalVolume)]
    [InlineData(DataSetKind.NearbyUniverse)]
    [InlineData(DataSetKind.Clusters)]
    [InlineData(DataSetKind.Quasars)]
    public async Task Builders_IdsFollowFormat(DataSetKind kind)
    {
        var first = await Build(Make(kind));
        var second = await Build(Make(kind));
        Assert.Equal(kind, first.Kind);
        var rx = new Regex(@"^(UNGC|MCC|2MRS|MCXC|ABELL|MQ):\S.*$");
        Assert.NotEmpty(first.Objects);
        Assert.All(first.Objects, o => Assert.Matches(rx, o.Id));
        Assert.Equal(first.Objects.Count, first.Objects.Select(o => o.Id).Distinct().Count());
        Assert.Equal(first.Objects.Select(o => o.Id), second.Objects.Select(o => o.Id));
    }

    [Fact]
    public async Task Builders_IdsOfKnownObjects()
    {
        var lv = await Build(new LocalVolumeBuilder());
        var m31 = Assert.Single(lv.Objects, o => o.AltNames.Contains("M 31"));
        Assert.Equal("MCC:Andromeda", m31.Id); // McConnachie wins the merge

        var clusters = await Build(new ClusterBuilder());
        Assert.Contains(clusters.Objects, o => o.Id == "MCXC:J1259.7+2756");
    }

    [Fact]
    public async Task LocalVolume_ExcludesObjectsWithoutDistance()
    {
        // McConnachie's "The Galaxy" row has a position but neither distance nor velocity.
        var ds = await Build(new LocalVolumeBuilder());
        Assert.DoesNotContain(ds.Objects, o => o.Name == "The Galaxy");
        Assert.True(ds.SkippedRows + ds.ExcludedCount >= 1);
    }
}
