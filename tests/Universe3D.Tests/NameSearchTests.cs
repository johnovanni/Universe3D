using Universe3D.Core.Catalogs;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;

namespace Universe3D.Tests;

public class NameSearchTests
{
    private static readonly string NoCache =
        Path.Combine(Path.GetTempPath(), "Universe3D.NoCache." + Guid.NewGuid().ToString("N"));

    private static IReadOnlyList<CosmicObject> Load(DataSetKind kind) =>
        new SnapshotStore(NoCache).Load(kind, out _, out _).Objects;

    private static Galaxy G(string name, params string[] alt) => new() { Id = "T:" + name, Name = name, AltNames = alt };

    /// <summary>Review Focus 5, on the built-in snapshots.</summary>
    [Fact]
    public void Search_NormalizesNames()
    {
        var local = new NameSearch(Load(DataSetKind.LocalVolume));
        var m31 = local.Find("m 31")[0];
        Assert.Equal("Andromeda Galaxy", m31.Name);
        Assert.Same(m31, local.Find("NGC0224")[0]);
        Assert.Same(m31, local.Find("andromeda")[0]);

        var quasars = new NameSearch(Load(DataSetKind.Quasars));
        Assert.Equal("3C 273", quasars.Find("3c273")[0].Name);

        var clusters = new NameSearch(Load(DataSetKind.Clusters));
        Assert.Contains("Coma Cluster", clusters.Find("coma")[0].Name);
    }

    [Fact]
    public void Search_IgnoresCaseSpacesUnderscoresAndLeadingZeros()
    {
        var target = G("NGC 4486", "M 87", "Virgo A");
        var s = new NameSearch(new CosmicObject[] { G("NGC 4487"), target, G("PGC 1") });
        Assert.Same(target, s.Find("ngc_04486")[0]);
        Assert.Same(target, s.Find("M087")[0]);
        Assert.Same(target, s.Find("virgoa")[0]);
        Assert.Same(target, s.Find("  Virgo   A ")[0]);
    }

    [Fact]
    public void Search_ExactThenPrefixThenContains()
    {
        var contains = G("Big Coma Group");
        var longPrefix = G("Coma Berenices dwarf");
        var shortPrefix = G("Coma Berenices");
        var exact = G("Abell 1656 (Coma Cluster)", "Coma");
        var s = new NameSearch(new CosmicObject[] { contains, longPrefix, shortPrefix, exact });

        var found = s.Find("coma");

        Assert.Equal(new CosmicObject[] { exact, shortPrefix, longPrefix, contains }, found);
    }

    [Fact]
    public void Search_EachObjectOnceAndCapped()
    {
        var objects = Enumerable.Range(1, 200).Select(i => (CosmicObject)G($"PGC {i}", $"PGC{i} alt")).ToList();
        var s = new NameSearch(objects);
        var found = s.Find("pgc", max: 50);
        Assert.Equal(50, found.Count);
        Assert.Equal(found.Count, found.Distinct().Count());
    }

    [Fact]
    public void Search_EmptyQueryFindsNothing()
    {
        var s = new NameSearch(new CosmicObject[] { G("M 31") });
        Assert.Empty(s.Find(""));
        Assert.Empty(s.Find("  _ "));
        Assert.Empty(s.Find("no such thing"));
    }

    [Theory]
    [InlineData("NGC 0224", "ngc224")]
    [InlineData("M_031", "m31")]
    [InlineData("3C 273", "3c273")]
    [InlineData("MCXC J0000.1+0816", "mcxcj0.1+816")]
    [InlineData("Abell 1656 (Coma Cluster)", "abell1656(comacluster)")]
    public void Normalize_Examples(string raw, string expected) =>
        Assert.Equal(expected, NameSearch.Normalize(raw));
}
