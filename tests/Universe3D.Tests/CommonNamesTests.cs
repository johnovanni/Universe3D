using Universe3D.Core.Catalogs;

namespace Universe3D.Tests;

public class CommonNamesTests
{
    [Theory]
    [InlineData("NGC0224", "NGC 224")]
    [InlineData("NGC_0253", "NGC 253")]
    [InlineData("MESSIER_031", "M 31")]
    [InlineData("UGC00064", "UGC 64")]
    [InlineData("IC0342", "IC 342")]
    [InlineData("ESO409-015", "ESO409-015")]
    [InlineData("A1656", "Abell 1656")]
    [InlineData("ACO 1656", "Abell 1656")]
    [InlineData("  NGC 4486 ", "NGC 4486")]
    [InlineData("Something odd", "Something odd")]
    public void CommonNames_Normalize(string raw, string expected)
        => Assert.Equal(expected, CommonNames.NormalizeDesignation(raw));

    [Fact]
    public void CommonNames_MessierCount()
    {
        Assert.Equal(40, CommonNames.Messier.Count);
        Assert.Equal("NGC 4486", CommonNames.Messier["M87"]);
        Assert.Equal("NGC 205", CommonNames.Messier["M110"]);
    }

    [Fact]
    public void CommonNames_PopularNames()
    {
        Assert.Equal("Andromeda Galaxy", CommonNames.Popular["NGC 224"]);
        Assert.Equal("Coma Cluster", CommonNames.Popular["Abell 1656"]);
        Assert.Equal("Large Magellanic Cloud", CommonNames.Popular["LMC"]);
        Assert.Equal("Virgo Cluster", CommonNames.Popular["Virgo"]);
        Assert.Equal("Fornax Cluster", CommonNames.Popular["Fornax"]);
    }

    [Fact]
    public void CommonNames_ClusterPositions()
    {
        var virgo = Assert.Single(CommonNames.ClusterPositions, p => p.Name == "Virgo Cluster");
        Assert.Equal(187.70, virgo.RaDeg);
        Assert.Equal(12.39, virgo.DecDeg);
        var fornax = Assert.Single(CommonNames.ClusterPositions, p => p.Name == "Fornax Cluster");
        Assert.Equal(54.62, fornax.RaDeg);
        Assert.Equal(-35.45, fornax.DecDeg);
    }
}
