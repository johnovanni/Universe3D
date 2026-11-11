using Universe3D.Core.Text;

namespace Universe3D.Tests;

public class GlossaryTests
{
    public static IEnumerable<object[]> RequiredKeys() =>
        new[]
        {
            "Redshift", "Blueshift", "Observed", "PeculiarVelocity", "ComovingDistance", "LookbackTime",
            "HubbleType", "AxisRatio", "PositionAngle", "Inclination", "M500", "R500",
            "Quasar", "Agn", "BlLac", "Type2Agn", "Cz",
            "Supergalactic", "Galactic", "Equatorial", "Megaparsec", "LightYear", "LineOfSightOnly",
        }.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void Glossary_HasEveryRequiredTerm(string key)
    {
        Assert.True(Glossary.All.ContainsKey(key), key);
        Assert.True(Glossary.Get(key).Length > 20, key);
    }

    [Fact]
    public void Glossary_PinnedTexts()
    {
        Assert.Equal(
            "What telescopes measure: the galaxy's own motion plus the expansion of the universe, which carries distant galaxies away from us.",
            Glossary.Get("Observed"));
        Assert.Equal(
            "The galaxy's own motion through space, with the universe's expansion removed. 'Peculiar' means particular to this galaxy, not strange. It is usually caused by the gravity of nearby galaxies and clusters.",
            Glossary.Get("PeculiarVelocity"));
    }

    [Fact]
    public void Glossary_UnknownKeyThrows() =>
        Assert.Throws<KeyNotFoundException>(() => Glossary.Get("NoSuchTerm"));
}
