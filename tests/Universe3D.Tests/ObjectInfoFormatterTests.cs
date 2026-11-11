using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using Universe3D.Core.Shapes;
using Universe3D.Core.Text;

namespace Universe3D.Tests;

public class ObjectInfoFormatterTests
{
    private static readonly CosmologyModel Cosmo = CosmologyModel.Default;

    /// <summary>Ruling R5: a synthetic M31 (0.77 Mpc, UNGC, cz −300, Sb), not snapshot data.</summary>
    private static Galaxy M31(double? pa = 35) => new()
    {
        Id = "UNGC:NGC0224",
        Name = "Andromeda Galaxy",
        AltNames = new[] { "M 31", "NGC 224", "Andromeda Galaxy" },
        RaDeg = 10.6847,
        DecDeg = 41.2690,
        DistanceMpc = 0.77,
        DistanceSource = DistanceSource.Measured,
        DistanceCatalog = "UNGC",
        CzHelio = -300,
        Z = -300 / RedshiftMath.C,
        VCmb = -584,
        PeculiarKms = -117,
        LookbackGyr = Cosmo.LookbackTimeGyr(Cosmo.RedshiftAtComovingDistance(0.77)),
        Morphology = MorphologyParser.FromHyperLeda("Sb"),
        PaDeg = pa,
        AxisRatio = 0.33,
        InclinationDeg = 78,
        InclinationMeasured = true,
        DiameterKpc = 43.4,
        Sources = new[] { "UNGC", "HyperLEDA" },
    };

    private static InfoRow Row(IReadOnlyList<InfoRow> rows, string label) =>
        Assert.Single(rows, r => r.Label == label);

    [Fact]
    public void Info_M31Rows()
    {
        var rows = ObjectInfoFormatter.Format(M31(), Cosmo);

        Assert.Contains(rows, r => r.Label == "Distance" && r.Value == "2.51 Mly (0.77 Mpc), measured (UNGC)");
        Assert.Contains(rows, r => r.Value.StartsWith("Blueshifted: approaching at 300 km/s", StringComparison.Ordinal));
        Assert.Contains(new InfoRow("Velocity arrow", "Line-of-sight motion only", "LineOfSightOnly"), rows);
        Assert.Contains("Sb", Row(rows, "Type").Value);
        Assert.Contains(rows, r => r.Value.Contains("tilt direction assumed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ruling R35: the stated tilt direction must match <see cref="GalaxyOrientation.DiskNormal"/>. The disk's north
    /// edge (the in-disk direction closest to sky north) lies farther from the Sun than the centre, so the text says
    /// "away" and never "toward us".
    /// </summary>
    [Fact]
    public void Info_TiltWordingMatchesDiskNormal()
    {
        var g = M31();
        var normal = GalaxyOrientation.DiskNormal(g.RaDeg, g.DecDeg, g.PaDeg!.Value, g.InclinationDeg!.Value);
        double ra = g.RaDeg * Math.PI / 180;
        var s = SkyMath.UnitVector(g.RaDeg, g.DecDeg);
        var east = new Vector3d(-Math.Sin(ra), Math.Cos(ra), 0);
        var north = s.Cross(east);
        var northEdge = (north - normal * north.Dot(normal)).Normalized(); // in the disk plane, toward sky north
        Assert.True(northEdge.Dot(north) > 0);

        var centre = g.PositionEq;
        var edgePoint = centre + northEdge * 0.01; // 10 kpc out along the disk
        Assert.True(edgePoint.Length > centre.Length, "the north edge is the far edge");

        string tilt = Row(ObjectInfoFormatter.Format(g, Cosmo), "Disk tilt").Value;
        Assert.Contains("north edge tilted away from us", tilt);
        Assert.DoesNotContain("toward us", tilt);
        Assert.Contains("tilted away from us", Glossary.Get("DiskTilt"));
        Assert.DoesNotContain("toward us", Glossary.Get("DiskTilt"));
    }

    [Fact]
    public void Info_QuasarHasLookback()
    {
        var q = new Quasar
        {
            Id = "MQ:TEST", Name = "Test quasar", RaDeg = 150, DecDeg = 2, Z = 7,
            DistanceMpc = Cosmo.ComovingDistanceMpc(7), DistanceSource = DistanceSource.Redshift,
            DistanceCatalog = "Redshift", Type = QuasarType.Quasar, RMag = 19.5,
        };

        var rows = ObjectInfoFormatter.Format(q, Cosmo);

        Assert.Equal("12.7 billion years", Row(rows, "Look-back time").Value);
        // z > 0.1: look-back time is the headline figure, and the shift is cosmic expansion.
        Assert.Equal("Look-back time", rows[0].Label);
        Assert.Contains(rows, r => r.Value.Contains("cosmic expansion", StringComparison.Ordinal));
    }

    [Fact]
    public void Info_MeasuredDistanceLookbackUsesDistance()
    {
        // Ruling R8: M31 is blueshifted, but its light still travelled about 2.5 million years.
        var rows = ObjectInfoFormatter.Format(M31(), Cosmo);
        Assert.Equal("2.51 million years", Row(rows, "Look-back time").Value);
    }

    [Fact]
    public void Info_EveryGlossaryKeyExists()
    {
        var objects = new CosmicObject[]
        {
            M31(), M31(pa: null),
            new GalaxyCluster { Id = "MCXC:X", Name = "Abell 1656 (Coma Cluster)", Z = 0.0231, CzHelio = 6925,
                DistanceMpc = 102, DistanceSource = DistanceSource.Redshift, DistanceCatalog = "Redshift",
                M500e14 = 4.28, R500Mpc = 1.14, L500e37W = 3.46, Richness = 2, BautzMorgan = "II",
                Catalogs = ClusterCatalogs.Mcxc | ClusterCatalogs.Abell },
            new Quasar { Id = "MQ:3C 273", Name = "3C 273", Z = 0.158, DistanceMpc = 652,
                DistanceSource = DistanceSource.Redshift, Type = QuasarType.BlLac, RMag = 14.1, BMag = 13.7 },
        };
        foreach (var o in objects)
            foreach (var r in ObjectInfoFormatter.Format(o, Cosmo))
                if (r.GlossaryKey is { } key)
                    Assert.True(Glossary.All.ContainsKey(key), $"{r.Label}: {key}");
    }

    [Fact]
    public void Info_UnknownMorphology_SaysNotClassified()
    {
        // Ruling R19.
        var g = new Galaxy
        {
            Id = "2MRS:X", Name = "PGC 1", DistanceMpc = 50, Z = 0.012, CzHelio = 3600,
            DistanceSource = DistanceSource.Redshift, DistanceCatalog = "Redshift",
            Morphology = MorphologyParser.FromZcat("98"),
        };
        var type = Row(ObjectInfoFormatter.Format(g, Cosmo), "Type");
        Assert.StartsWith("Type not classified in the catalog", type.Value);
        Assert.DoesNotContain("Unknown", type.Value);
    }

    [Fact]
    public void Info_NoPositionAngle_NotMeasuredAndIllustrative()
    {
        // Ruling R27.
        var rows = ObjectInfoFormatter.Format(M31(pa: null), Cosmo);
        Assert.Equal("not measured", Row(rows, "Position angle").Value);
        Assert.Contains(rows, r => r.Value.Contains("illustrative", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, r => r.Value.Contains("tilt direction assumed", StringComparison.Ordinal));
    }

    [Fact]
    public void Info_NoShapeData_InclinationNotMeasured()
    {
        var g = new Galaxy
        {
            Id = "UNGC:X", Name = "KK 1", DistanceMpc = 3, DistanceSource = DistanceSource.Measured,
            DistanceCatalog = "UNGC", Morphology = MorphologyParser.FromUngc(10, "L"),
        };
        var rows = ObjectInfoFormatter.Format(g, Cosmo);
        Assert.Equal("not measured", Row(rows, "Position angle").Value);
        Assert.Equal("not measured", Row(rows, "Inclination").Value);
    }

    [Fact]
    public void Info_ClusterRows()
    {
        var c = new GalaxyCluster
        {
            Id = "MCXC:J1259.7+2756", Name = "Abell 1656 (Coma Cluster)",
            AltNames = new[] { "Abell 1656", "Coma Cluster" }, Z = 0.0231, CzHelio = 6925,
            DistanceMpc = 102.2, DistanceSource = DistanceSource.Redshift, DistanceCatalog = "Redshift",
            M500e14 = 4.2846, R500Mpc = 1.1378, Catalogs = ClusterCatalogs.Mcxc | ClusterCatalogs.Abell,
            Sources = new[] { "MCXC", "Abell" },
        };
        var rows = ObjectInfoFormatter.Format(c, Cosmo);
        Assert.Equal(new InfoRow("Mass M500", "4.28 × 10¹⁴ solar masses", "M500"), Row(rows, "Mass M500"));
        Assert.Equal("3.71 Mly (1.14 Mpc)", Row(rows, "Radius R500").Value);
        Assert.Equal("333 Mly (102 Mpc), from redshift", Row(rows, "Distance").Value);
        Assert.Equal("Redshifted: receding at 6,925 km/s", Row(rows, "Motion").Value);
        Assert.Equal("MCXC, Abell", Row(rows, "Data sources").Value);
        Assert.DoesNotContain(rows, r => r.Label == "Peculiar velocity");
    }

    [Fact]
    public void Info_PeculiarVelocityShownForMeasuredLowZ()
    {
        var row = Row(ObjectInfoFormatter.Format(M31(), Cosmo), "Peculiar velocity");
        Assert.Equal("PeculiarVelocity", row.GlossaryKey);
        Assert.StartsWith("117 km/s toward us", row.Value);
        Assert.Contains("relative to the Local Group", row.Value);
    }

    private static Galaxy Cf4Galaxy(double distanceMpc, double? pec) => new()
    {
        Id = "2MRS:X", Name = "NGC 1", RaDeg = 150, DecDeg = 20, DistanceMpc = distanceMpc,
        DistanceSource = DistanceSource.Measured, DistanceCatalog = "Cosmicflows-4",
        CzHelio = 70 * distanceMpc, Z = 70 * distanceMpc / RedshiftMath.C, VCmb = 70 * distanceMpc + 200,
        PeculiarKms = pec, Morphology = MorphologyParser.FromHyperLeda("Sb"),
    };

    [Fact]
    public void Info_PeculiarVelocity_Cosmicflows4_NamesTheCmbFrame()
    {
        var row = Row(ObjectInfoFormatter.Format(Cf4Galaxy(50, 470), Cosmo), "Peculiar velocity");
        Assert.StartsWith("470 km/s away from us", row.Value);
        Assert.Contains("relative to the cosmic microwave background", row.Value);
    }

    [Fact]
    public void Info_PeculiarVelocity_Cosmicflows4_BeyondLimit_NotReliable()
    {
        var row = Row(ObjectInfoFormatter.Format(Cf4Galaxy(200, null), Cosmo), "Peculiar velocity");
        Assert.Equal("not reliable at this distance", row.Value);
        Assert.Equal("PeculiarVelocity", row.GlossaryKey);
    }
}
