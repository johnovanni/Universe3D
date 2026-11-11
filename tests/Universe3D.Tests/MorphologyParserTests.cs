using Universe3D.Core.Morphology;

namespace Universe3D.Tests;

public class MorphologyParserTests
{
    [Theory]
    [InlineData(" 3A2s", MorphologyClass.Spiral, "Sb", "3A2s")]
    [InlineData(" 5X_s", MorphologyClass.Spiral, "Sc", "5X_s")]
    [InlineData(" 5X_s", MorphologyClass.Spiral, "mixed bar", "5X_s")]
    [InlineData(" 1B", MorphologyClass.BarredSpiral, "Sa", "1B")]
    [InlineData("-5", MorphologyClass.Elliptical, "", "-5")]
    [InlineData("-2A_R", MorphologyClass.Lenticular, "", "-2A_R")]
    [InlineData("10", MorphologyClass.Irregular, "", "10")]
    [InlineData("-5__P", MorphologyClass.Elliptical, "", "-5__P")]
    [InlineData("", MorphologyClass.Unknown, "", "")]
    public void Zcat(string code, MorphologyClass cls, string contains, string original)
    {
        var info = MorphologyParser.FromZcat(code);
        Assert.Equal(cls, info.Class);
        Assert.Contains(contains, info.Description);
        Assert.Equal(original, info.OriginalCode);
    }

    [Fact]
    public void ZcatPeculiarFlag()
    {
        Assert.True(MorphologyParser.FromZcat("-5__P").IsPeculiar);
        Assert.False(MorphologyParser.FromZcat("-5").IsPeculiar);
    }

    [Fact]
    public void ZcatShortAndUnparseableCodes()
    {
        Assert.Equal(MorphologyClass.Spiral, MorphologyParser.FromZcat(" 3").Class);
        Assert.Equal(MorphologyClass.Unknown, MorphologyParser.FromZcat("??").Class);
        Assert.Equal(MorphologyClass.Unknown, MorphologyParser.FromZcat("99").Class);
        Assert.Equal(MorphologyClass.Unknown, MorphologyParser.FromZcat(null).Class);
    }

    [Fact]
    public void ZcatDescriptionIsReadable() =>
        Assert.Equal("Sb spiral (unbarred)", MorphologyParser.FromZcat(" 3A2s").Description);

    [Theory]
    [InlineData(5, "", MorphologyClass.Spiral, "Sc")]
    [InlineData(-3, "X", MorphologyClass.DwarfSpheroidal, "")]
    [InlineData(10, "L", MorphologyClass.DwarfIrregular, "")]
    [InlineData(10, "", MorphologyClass.Irregular, "")]
    [InlineData(8, "", MorphologyClass.Spiral, "Sdm")]
    [InlineData(-5, "", MorphologyClass.Elliptical, "")]
    [InlineData(-2, "", MorphologyClass.Lenticular, "")]
    public void Ungc(int t, string tdw, MorphologyClass cls, string contains)
    {
        var info = MorphologyParser.FromUngc(t, tdw);
        Assert.Equal(cls, info.Class);
        Assert.Contains(contains, info.Description);
    }

    [Fact]
    public void UngcMissingTIsUnknown()
    {
        Assert.Equal(MorphologyClass.Unknown, MorphologyParser.FromUngc(null, null).Class);
        Assert.Equal(MorphologyClass.DwarfSpheroidal, MorphologyParser.FromUngc(-3, " N ").Class);
    }

    [Theory]
    [InlineData("dSph", MorphologyClass.DwarfSpheroidal, "")]
    [InlineData("dIrr", MorphologyClass.DwarfIrregular, "")]
    [InlineData("dIrr/dSph", MorphologyClass.DwarfIrregular, "transition")]
    [InlineData("Sb", MorphologyClass.Spiral, "")]
    [InlineData("Irr", MorphologyClass.Irregular, "")]
    [InlineData("dE4", MorphologyClass.DwarfSpheroidal, "")]
    [InlineData("cE2", MorphologyClass.Elliptical, "")]
    public void McConnachie(string mType, MorphologyClass cls, string contains)
    {
        var info = MorphologyParser.FromMcConnachie(mType);
        Assert.Equal(cls, info.Class);
        Assert.Contains(contains, info.Description);
        Assert.Equal(mType, info.OriginalCode);
    }

    [Theory]
    [InlineData("Sbc", MorphologyClass.Spiral)]
    [InlineData("SBb", MorphologyClass.BarredSpiral)]
    [InlineData("E", MorphologyClass.Elliptical)]
    [InlineData("SO-a", MorphologyClass.Lenticular)]
    [InlineData("E-SO", MorphologyClass.Lenticular)]
    [InlineData("SO", MorphologyClass.Lenticular)]
    [InlineData("Irr", MorphologyClass.Irregular)]
    [InlineData("E?", MorphologyClass.Elliptical)]
    [InlineData("S?", MorphologyClass.Spiral)]
    [InlineData("Sab", MorphologyClass.Spiral)]
    [InlineData("Sdm", MorphologyClass.Spiral)]
    [InlineData("Sm", MorphologyClass.Spiral)]
    [InlineData("SBm", MorphologyClass.BarredSpiral)]
    [InlineData("SBbc", MorphologyClass.BarredSpiral)]
    [InlineData("SBcd", MorphologyClass.BarredSpiral)]
    [InlineData("-----", MorphologyClass.Unknown)]
    [InlineData("", MorphologyClass.Unknown)]
    [InlineData("  ", MorphologyClass.Unknown)]
    public void HyperLeda(string mType, MorphologyClass cls)
    {
        var info = MorphologyParser.FromHyperLeda(mType);
        Assert.Equal(cls, info.Class);
        Assert.Equal(mType.Trim(), info.OriginalCode);
    }

    [Fact]
    public void HyperLedaUncertainSpiralHasUnknownSubtype() =>
        Assert.Contains("subtype unknown", MorphologyParser.FromHyperLeda("S?").Description);
}
