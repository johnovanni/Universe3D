using Universe3D.Core.Display;
using Universe3D.Core.Objects;

namespace Universe3D.Tests;

public class LabelPriorityTests
{
    [Fact]
    public void Galaxies_MessierNgcFirstByBrightnessThenOthers()
    {
        var objects = new CosmicObject[]
        {
            new Galaxy { Id = "a", Name = "PGC 5", Magnitude = 1 },                               // 0: bright, unnamed
            new Galaxy { Id = "b", Name = "NGC 253", Magnitude = 3.8 },                           // 1
            new Galaxy { Id = "c", Name = "Andromeda Galaxy", AltNames = new[] { "M 31" }, Magnitude = 0.8 }, // 2
            new Galaxy { Id = "d", Name = "The Galaxy", AltNames = new[] { "Milky Way" } },      // 3: never labeled
            new Galaxy { Id = "e", Name = "PGC 9", Magnitude = 9 },                               // 4
            new Galaxy { Id = "f", Name = "UGC 1", DiameterKpc = 30 },                            // 5: no magnitude
        };
        Assert.Equal(new[] { 2, 1, 0, 4, 5 }, LabelPriority.Order(objects));
    }

    [Fact]
    public void LocalVolume_WithoutMagnitudes_UsesSize()
    {
        var objects = new CosmicObject[]
        {
            new Galaxy { Id = "a", Name = "KK 1", DiameterKpc = 1 },
            new Galaxy { Id = "b", Name = "Large Magellanic Cloud", DiameterKpc = 9 },
            new Galaxy { Id = "c", Name = "NGC 55", DiameterKpc = 20 },
            new Galaxy { Id = "d", Name = "NGC 300", DiameterKpc = 25 },
        };
        Assert.Equal(new[] { 3, 2, 1, 0 }, LabelPriority.Order(objects));
    }

    [Fact]
    public void Clusters_NamedFirst()
    {
        var objects = new CosmicObject[]
        {
            new GalaxyCluster { Id = "a", Name = "MCXC J0000.1+0816", M500e14 = 9 },
            new GalaxyCluster { Id = "b", Name = "Abell 1656 (Coma Cluster)", M500e14 = 4 },
            new GalaxyCluster { Id = "c", Name = "Abell 2", M500e14 = 1 },
        };
        Assert.Equal(new[] { 1, 0, 2 }, LabelPriority.Order(objects));
    }

    [Fact]
    public void Quasars_3CAndPksFirst()
    {
        var objects = new CosmicObject[]
        {
            new Quasar { Id = "a", Name = "SDSS J1", Magnitude = 14 },
            new Quasar { Id = "b", Name = "PKS 2155-304", Magnitude = 15 },
            new Quasar { Id = "c", Name = "3C 273", Magnitude = 14.1 },
        };
        Assert.Equal(new[] { 2, 1, 0 }, LabelPriority.Order(objects));
    }
}
