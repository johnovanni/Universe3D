using System.Diagnostics;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using Universe3D.Rendering;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Tests;

[Collection(PerfCollection.Name)]
public class ShapeSelectionTests
{
    private const double MaxDistance = 15;
    // Spirals (class 3) are hidden.
    private const uint TypeMask = ~(1u << (int)MorphologyClass.Spiral);

    /// <summary>Eye at the Sun looking along +x (equatorial RA 0, Dec 0), 1600x900 px.</summary>
    private static Projector SunProjector()
    {
        var cam = new OrbitCamera { Target = new Vector3d(10, 0, 0), DistanceMpc = 10, YawDeg = 180, PitchDeg = 0 };
        Assert.Equal(0, cam.Eye.Length, 1e-9);
        return new Projector(cam, 1600, 900);
    }

    /// <summary>
    /// 1,000 galaxies: most inside a narrow cone in front of the camera (all on screen), 1-20 Mpc away,
    /// 0.5-60 kpc across, every morphology class; some without a diameter, some behind the camera.
    /// </summary>
    private static List<Galaxy> Synthetic(int n = 1000, int seed = 7)
    {
        var rng = new Random(seed);
        var classes = Enum.GetValues<MorphologyClass>();
        var list = new List<Galaxy>(n);
        for (int i = 0; i < n; i++)
        {
            bool behind = i % 50 == 0;
            list.Add(new Galaxy
            {
                Id = $"TEST:{i}",
                RaDeg = (behind ? 180 : 0) + rng.NextDouble() * 16 - 8,
                DecDeg = rng.NextDouble() * 16 - 8,
                DistanceMpc = 1 + rng.NextDouble() * 19,
                DiameterKpc = i % 17 == 0 ? null : 0.5 + rng.NextDouble() * 59.5,
                Morphology = new MorphologyInfo(classes[rng.Next(classes.Length)], "", "", false),
            });
        }
        return list;
    }

    private static double Px(Projector proj, Galaxy g) => proj.PixelSize(g.DiameterKpc!.Value / 1000, g.PositionEq);

    private static bool Eligible(Projector proj, Galaxy g) =>
        g.DiameterKpc is double && g.DistanceMpc <= MaxDistance &&
        ((TypeMask >> (int)g.TypeCode) & 1) != 0 && Px(proj, g) > 4;

    [Fact]
    public void ShapeSelection_RespectsCapAndSize()
    {
        var galaxies = Synthetic();
        var proj = SunProjector();
        const int cap = 50;
        int eligible = galaxies.Count(g => Eligible(proj, g));
        Assert.True(eligible > cap, $"the synthetic set should exceed the cap ({eligible} eligible)");

        var selected = ShapeRenderer.SelectInstances(galaxies, proj, MaxDistance, TypeMask, cap);

        Assert.Equal(cap, selected.Count);
        Assert.Equal(selected.Count, selected.Distinct().Count());
        var px = selected.Select(i => Px(proj, galaxies[i])).ToArray();
        foreach (int i in selected)
        {
            var g = galaxies[i];
            Assert.NotNull(g.DiameterKpc);
            Assert.True(Px(proj, g) > 4, $"{g.Id} is {Px(proj, g):F2} px");
            Assert.True(g.DistanceMpc <= MaxDistance, $"{g.Id} is {g.DistanceMpc:F1} Mpc away");
            Assert.True(((TypeMask >> (int)g.TypeCode) & 1) != 0, $"{g.Id} is a hidden {g.Morphology.Class}");
        }
        for (int k = 1; k < px.Length; k++)
            Assert.True(px[k - 1] >= px[k], $"not sorted by size at {k}: {px[k - 1]:F2} < {px[k]:F2}");

        // Largest first: nothing left out is bigger than the smallest one taken.
        var taken = selected.ToHashSet();
        double smallest = px[^1];
        for (int i = 0; i < galaxies.Count; i++)
            if (!taken.Contains(i) && Eligible(proj, galaxies[i]))
                Assert.True(Px(proj, galaxies[i]) <= smallest, $"{galaxies[i].Id} ({Px(proj, galaxies[i]):F2} px) was skipped");
    }

    [Fact]
    public void ShapeSelection_WithoutCapPressure_TakesEveryEligibleGalaxy()
    {
        var galaxies = Synthetic();
        var proj = SunProjector();
        var expected = Enumerable.Range(0, galaxies.Count).Where(i => Eligible(proj, galaxies[i])).ToHashSet();

        var selected = ShapeRenderer.SelectInstances(galaxies, proj, MaxDistance, TypeMask, 20_000);

        Assert.Equal(expected, selected.ToHashSet());
        Assert.DoesNotContain(selected, i => galaxies[i].RaDeg > 90);   // behind the camera
    }

    [Fact]
    public void ShapeSelection_SkipsGalaxiesOffScreen()
    {
        // A big, close galaxy far to the side of the view is not on screen.
        var proj = SunProjector();
        var side = new Galaxy { Id = "TEST:side", RaDeg = 80, DecDeg = 0, DistanceMpc = 2, DiameterKpc = 50,
                                Morphology = new MorphologyInfo(MorphologyClass.Elliptical, "", "", false) };
        var ahead = new Galaxy { Id = "TEST:ahead", RaDeg = 0, DecDeg = 0, DistanceMpc = 2, DiameterKpc = 50,
                                 Morphology = new MorphologyInfo(MorphologyClass.Elliptical, "", "", false) };
        Assert.True(proj.PixelSize(0.05, side.PositionEq) > 4);

        var selected = ShapeRenderer.SelectInstances(new[] { side, ahead }, proj, 100, uint.MaxValue, 10);

        Assert.Equal(new[] { 1 }, selected);
    }

    private static OrbitCamera SunCamera() =>
        new() { Target = new Vector3d(10, 0, 0), DistanceMpc = 10, YawDeg = 180, PitchDeg = 0 };

    private static ShapeLod Lod(IReadOnlyList<Galaxy>? galaxies = null) =>
        new(new ShapeCandidates(galaxies ?? Synthetic(), Universe3D.Core.Coordinates.DisplayFrame.Equatorial));

    [Fact]
    public void ShapeLod_HiddenThenShownWithUnchangedCamera_ReselectsAndRestoresTheWindow()
    {
        var lod = Lod();
        var cam = SunCamera();
        Assert.True(lod.Update(true, cam, 1600, 900, MaxDistance, TypeMask, 20_000));
        Assert.Equal(new OpenTK.Mathematics.Vector2(4, 8), lod.FadeRangePx);
        int shown = lod.Selected.Count;
        Assert.True(shown > 0);

        Assert.False(lod.Update(true, cam, 1600, 900, MaxDistance, TypeMask, 20_000)); // cached
        Assert.Equal(new OpenTK.Mathematics.Vector2(4, 8), lod.FadeRangePx);

        Assert.True(lod.Update(false, cam, 1600, 900, MaxDistance, TypeMask, 20_000)); // hidden: nothing drawn
        Assert.Equal(OpenTK.Mathematics.Vector2.Zero, lod.FadeRangePx);
        Assert.Empty(lod.Selected);

        Assert.True(lod.Update(true, cam, 1600, 900, MaxDistance, TypeMask, 20_000));   // shown again, same camera
        Assert.Equal(new OpenTK.Mathematics.Vector2(4, 8), lod.FadeRangePx);
        Assert.Equal(shown, lod.Selected.Count);
    }

    [Fact]
    public void ShapeLod_CapLeavesGalaxiesOut_WindowStartsAtTheSmallestShape()
    {
        var galaxies = Synthetic();
        var lod = Lod(galaxies);
        var cam = SunCamera();
        Assert.True(lod.Update(true, cam, 1600, 900, MaxDistance, TypeMask, 50));
        Assert.Equal(50, lod.Selected.Count);
        double smallest = Px(new Projector(cam, 1600, 900), galaxies[lod.Selected[^1]]);
        Assert.True(smallest > 4);
        Assert.Equal(smallest, lod.FadeRangePx.X, smallest * 1e-6);
        Assert.Equal(2 * lod.FadeRangePx.X, lod.FadeRangePx.Y, 1e-3);
    }

    [Fact]
    public void ShapeLod_NoCandidates_NoShapesAndNoFade()
    {
        var noDiameters = Synthetic().Select(g => new Galaxy
        {
            Id = g.Id, RaDeg = g.RaDeg, DecDeg = g.DecDeg, DistanceMpc = g.DistanceMpc, Morphology = g.Morphology,
        }).ToList();
        foreach (var lod in new[] { Lod(noDiameters), Lod(Array.Empty<Galaxy>()), new ShapeLod(null) })
        {
            lod.Update(true, SunCamera(), 1600, 900, MaxDistance, TypeMask, 20_000);
            Assert.Equal(OpenTK.Mathematics.Vector2.Zero, lod.FadeRangePx);
            Assert.Empty(lod.Selected);
        }
    }

    [Theory]
    [InlineData(0, 4, 8)]
    [InlineData(2.5, 4, 8)]
    [InlineData(12, 12, 24)]
    public void ShapeLod_FadeWindow(double cutoffPx, float lo, float hi) =>
        Assert.Equal(new OpenTK.Mathematics.Vector2(lo, hi), ShapeLod.FadeWindow(cutoffPx));

    /// <summary>
    /// The instance's ellipsoid, projected on the sky as seen from the Sun, has the catalog PA and axis ratio:
    /// disks via the inclination with q0 = 0.2, ellipsoids directly. Built in the galactic frame and rotated back.
    /// </summary>
    [Theory]
    [InlineData(10.68, 41.27, 35, 0.473, MorphologyClass.Spiral)]          // M31
    [InlineData(23.46, 30.66, 22, 0.59, MorphologyClass.Spiral)]           // M33
    [InlineData(80.89, -69.76, 170, 0.85, MorphologyClass.Irregular)]      // LMC
    [InlineData(150.0, 2.0, 95, 0.3, MorphologyClass.Unknown)]
    [InlineData(187.71, 12.39, 160, 0.6, MorphologyClass.Elliptical)]
    [InlineData(250.0, -40.0, 10, 0.75, MorphologyClass.DwarfSpheroidal)]
    public void ShapeInstance_SeenFromTheSun_HasCatalogPaAndAxisRatio(double ra, double dec, double pa, double q,
                                                                      MorphologyClass cls)
    {
        bool disk = cls is not (MorphologyClass.Elliptical or MorphologyClass.DwarfSpheroidal);
        var g = new Galaxy
        {
            Id = "TEST:shape", RaDeg = ra, DecDeg = dec, DistanceMpc = 1, DiameterKpc = 20, PaDeg = pa, AxisRatio = q,
            InclinationDeg = disk ? Universe3D.Core.Shapes.GalaxyOrientation.InclinationDeg(q) : null,
            Morphology = new MorphologyInfo(cls, "", "", false),
        };
        var inst = ShapeRenderer.Instance(g, Universe3D.Core.Coordinates.DisplayFrame.Galactic);
        Assert.Equal(disk ? 0.2 * 0.01 : q * 0.01, inst.SemiAxesMpc.Z, 1e-6);

        // Semi-axis vectors, back in equatorial coordinates.
        var toEq = Universe3D.Core.Coordinates.SkyFrames.EquatorialToGalactic.Transpose();
        var rot = new System.Numerics.Quaternion(inst.Rotation.X, inst.Rotation.Y, inst.Rotation.Z, inst.Rotation.W);
        var semi = new[] { inst.SemiAxesMpc.X, inst.SemiAxesMpc.Y, inst.SemiAxesMpc.Z };
        var units = new[] { System.Numerics.Vector3.UnitX, System.Numerics.Vector3.UnitY, System.Numerics.Vector3.UnitZ };

        // Sky basis at the galaxy: east e, north n; the projected ellipse is P·Pᵀ with P = [e; n]·axes.
        double r = ra * Math.PI / 180;
        var s = Universe3D.Core.Coordinates.SkyMath.UnitVector(ra, dec);
        var e = new Vector3d(-Math.Sin(r), Math.Cos(r), 0);
        var n = s.Cross(e);
        double mee = 0, mnn = 0, men = 0;
        for (int k = 0; k < 3; k++)
        {
            var a = System.Numerics.Vector3.Transform(units[k], rot) * semi[k];
            var eq = toEq * new Vector3d(a.X, a.Y, a.Z);
            double pe = eq.Dot(e), pn = eq.Dot(n);
            mee += pe * pe; mnn += pn * pn; men += pe * pn;
        }
        double phi = 0.5 * Math.Atan2(2 * men, mee - mnn);     // major axis, from east toward north
        double paOut = (Math.Atan2(Math.Cos(phi), Math.Sin(phi)) * 180 / Math.PI + 180) % 180;
        double mid = (mee + mnn) / 2, dev = Math.Sqrt((mee - mnn) * (mee - mnn) / 4 + men * men);
        double qOut = Math.Sqrt((mid - dev) / (mid + dev));

        double dPa = Math.Abs(paOut - pa) % 180;
        Assert.True(Math.Min(dPa, 180 - dPa) < 0.5, $"PA {paOut:F2} vs {pa}");
        Assert.Equal(q, qOut, 0.01);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void ShapeSelection_NearbyUniverseSizedInputUnder5ms()
    {
        var rng = new Random(3);
        var galaxies = new List<Galaxy>(43_535);
        for (int i = 0; i < 43_535; i++)
            galaxies.Add(new Galaxy
            {
                Id = $"TEST:{i}", RaDeg = rng.NextDouble() * 360, DecDeg = rng.NextDouble() * 180 - 90,
                DistanceMpc = 0.5 + rng.NextDouble() * 30, DiameterKpc = 5 + rng.NextDouble() * 50,
                Morphology = new MorphologyInfo((MorphologyClass)rng.Next(9), "", "", false),
            });
        var proj = SunProjector();
        var candidates = new ShapeCandidates(galaxies, Universe3D.Core.Coordinates.DisplayFrame.Equatorial);
        candidates.Select(proj, 100, uint.MaxValue, 20_000); // warm-up (JIT)
        var times = new List<double>();
        for (int k = 0; k < 5; k++)
        {
            var sw = Stopwatch.StartNew();
            var sel = candidates.Select(proj, 100, uint.MaxValue, 20_000);
            times.Add(sw.Elapsed.TotalMilliseconds);
            Assert.NotEmpty(sel);
        }
        times.Sort();
        Assert.True(times[2] < 5, $"median {times[2]:F2} ms");
    }
}
