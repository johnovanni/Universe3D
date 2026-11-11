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
            // Added with the main UI (Task 13): filter checkboxes and info-panel fields.
            "MeasuredDistance", "CmbFrame", "Magnitude", "L500", "Richness", "BautzMorgan", "Mcxc", "Abell",
            "Elliptical", "Lenticular", "Spiral", "BarredSpiral", "Irregular", "DwarfSpheroidal", "DwarfIrregular",
            "PeculiarGalaxy", "Unclassified", "DiskTilt", "Diameter", "ScaleRings", "MilkyWay",
            "LocalGroup", "QuasarLimit", "NoValidDistance",
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

    /// <summary>Ruling R30: educational accuracy of the redshift, cz and M500 texts.</summary>
    [Fact]
    public void Glossary_AccuracyR30()
    {
        string redshift = Glossary.Get("Redshift");
        Assert.Contains("expansion", redshift);
        Assert.DoesNotContain("bigger z means a faster", redshift);

        string cz = Glossary.Get("Cz");
        Assert.DoesNotContain("close to the actual speed of recession", cz);
        Assert.Contains("distant", cz);
        Assert.Contains("Andromeda", cz);

        string m500 = Glossary.Get("M500");
        int dark = m500.IndexOf("dark matter", StringComparison.Ordinal);
        int gas = m500.IndexOf("hot gas", StringComparison.Ordinal);
        int stars = m500.IndexOf("stars", StringComparison.Ordinal);
        Assert.True(dark >= 0 && dark < gas && gas < stars, m500);
    }

    [Fact]
    public void Glossary_UnknownKeyThrows() =>
        Assert.Throws<KeyNotFoundException>(() => Glossary.Get("NoSuchTerm"));
}
