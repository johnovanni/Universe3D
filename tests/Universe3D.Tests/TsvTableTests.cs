using Universe3D.Core.Catalogs;

namespace Universe3D.Tests;

public class TsvTableTests
{
    internal const string Sample =
        "# VizieR comment\n" +
        "#Table\tfoo\n" +
        "\n" +
        "recno\tID\tRAJ2000\tcz\tn\n" +
        "\t \tdeg\tkm/s\t\n" +
        "--------\t----------------\t---------\t-----\t---\n" +
        "1\tNGC 1  \t  10.5\t 1200\t3\n" +
        "\n" +
        "2\tNGC 2\t11.25\t\t----\n" +
        "3\tNGC 3\t12.0\tNaN\tx\n";

    [Fact]
    public void TsvTable_ParsesBlanksAsNull()
    {
        var t = TsvTable.Parse(new StringReader(Sample));
        Assert.Equal(3, t.Count);
        Assert.Equal("NGC 1", t.Get(0, "ID"));
        Assert.Equal(10.5, t.GetDouble(0, "RAJ2000"));
        Assert.Equal(1200.0, t.GetDouble(0, "cz"));
        Assert.Equal(3, t.GetInt(0, "n"));
        Assert.Null(t.GetDouble(1, "cz"));
        Assert.Null(t.Get(1, "n"));
        Assert.Null(t.GetInt(1, "n"));
        Assert.Null(t.GetDouble(2, "cz"));
        Assert.Null(t.GetInt(2, "n"));
        Assert.Null(t.Get(0, "missing"));
    }
}
