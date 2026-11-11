using System.Diagnostics;
using System.Windows;
using Universe3D.Rendering;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Tests;

[Collection(PerfCollection.Name)]
public class PickerTests
{
    [Fact]
    public void Picker_PicksNearestVisible()
    {
        // Eye at (10, 0, 0) looking at the origin: world +y is screen right, +z is screen up.
        var cam = new OrbitCamera { Target = new Vector3d(0, 0, 0), DistanceMpc = 10, YawDeg = 0, PitchDeg = 0 };
        var proj = new Projector(cam, 800, 600);
        double mpcPerPx = 1.0 / proj.PixelSize(1.0, new Vector3d(0, 0, 0));
        var positions = new[]
        {
            new Vector3d(0, 3 * mpcPerPx, 0),   // 3 px right of the click
            new Vector3d(0, 0, 6 * mpcPerPx),   // 6 px above the click
            new Vector3d(0, 0, 0),              // exactly under the click, but hidden
        };
        Assert.True(proj.TryProject(positions[2], out var click));

        Assert.Equal(2, Picker.Pick(positions, _ => true, proj, click));
        Assert.Equal(0, Picker.Pick(positions, i => i != 2, proj, click));
        Assert.Equal(1, Picker.Pick(positions, i => i == 1, proj, click));

        var far = new Point(click.X - 10, click.Y + 10);
        Assert.Equal(-1, Picker.Pick(positions, _ => true, proj, far));
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void Picker_MillionPointsUnder15ms()
    {
        var rng = new Random(42);
        var positions = new Vector3d[1_000_000];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new Vector3d(rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100);
        var cam = new OrbitCamera { DistanceMpc = 300, YawDeg = 25, PitchDeg = 15 };
        var proj = new Projector(cam, 1600, 900);
        Func<int, bool> visible = i => (i & 7) != 0;

        Picker.Pick(positions, visible, proj, new Point(800, 450)); // warm-up (JIT)
        var times = new List<double>();
        for (int k = 0; k < 5; k++)
        {
            var sw = Stopwatch.StartNew();
            Picker.Pick(positions, visible, proj, new Point(700 + 40 * k, 400 + 20 * k));
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        Assert.True(times[2] < 15, $"median {times[2]:F2} ms ({string.Join(", ", times.Select(t => t.ToString("F2")))})");
    }
}
