using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>Screen-space picking of the visible point nearest to a click.</summary>
public static class Picker
{
    private const int ParallelThreshold = 100_000;

    static Picker()
    {
        if (Unsafe.SizeOf<Vector3d>() != 3 * sizeof(double))
            throw new PlatformNotSupportedException("Vector3d is expected to be three packed doubles.");
    }

    /// <summary>
    /// The index of the visible point nearest to the click within the radius, or −1. Ties go to the lower index.
    /// Large inputs are scanned in parallel (pass an array to avoid a copy); <paramref name="isVisible"/> is only
    /// called on the calling thread, for points that project inside the radius.
    /// </summary>
    public static int Pick(IReadOnlyList<Vector3d> positions, Func<int, bool> isVisible, Projector proj, Point click,
                           double radiusPx = 8)
    {
        var all = positions as Vector3d[] ?? positions.ToArray();
        var forms = proj.FormsFor(click);
        int n = all.Length;

        List<int>[] candidates;
        if (n < ParallelThreshold)
        {
            candidates = new[] { new List<int>() };
            Scan(all, 0, n, forms, radiusPx, candidates[0]);
        }
        else
        {
            int chunks = Math.Max(1, Environment.ProcessorCount);
            candidates = new List<int>[chunks];
            Parallel.For(0, chunks, c =>
            {
                int start = (int)((long)n * c / chunks), end = (int)((long)n * (c + 1) / chunks);
                var found = new List<int>();
                Scan(all, start, end, forms, radiusPx, found);
                candidates[c] = found;
            });
        }

        int best = -1;
        double bestD2 = radiusPx * radiusPx;
        foreach (var list in candidates)
        {
            foreach (int i in list)
            {
                if (!proj.TryProject(all[i], out var s)) continue;
                double dx = s.X - click.X, dy = s.Y - click.Y;
                double d2 = dx * dx + dy * dy;
                if (d2 > bestD2 || (d2 == bestD2 && best >= 0)) continue;
                if (!isVisible(i)) continue;
                best = i;
                bestD2 = d2;
            }
        }
        return best;
    }

    /// <summary>Collects indices whose projection falls inside the click's bounding square (no divisions).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Scan(Vector3d[] all, int start, int end, Projector.ClickForms f, double radiusPx,
                             List<int> found)
    {
        // Vector3d is three sequential doubles; reading them flat avoids per-point property calls.
        ReadOnlySpan<double> xyz = MemoryMarshal.Cast<Vector3d, double>(all.AsSpan(start, end - start));
        double ex = f.Eye.X, ey = f.Eye.Y, ez = f.Eye.Z;
        double w0 = f.W0, w1 = f.W1, w2 = f.W2, w3 = f.W3;
        double x0 = f.Dx0, x1 = f.Dx1, x2 = f.Dx2, x3 = f.Dx3;
        double y0 = f.Dy0, y1 = f.Dy1, y2 = f.Dy2, y3 = f.Dy3;
        for (int k = 0, i = start; k + 2 < xyz.Length; k += 3, i++)
        {
            double x = xyz[k] - ex, y = xyz[k + 1] - ey, z = xyz[k + 2] - ez;
            double w = x * w0 + y * w1 + z * w2 + w3;
            if (w <= 0) continue;
            double r = radiusPx * w;
            double dxw = x * x0 + y * x1 + z * x2 + x3;
            if (dxw > r || dxw < -r) continue;
            double dyw = x * y0 + y * y1 + z * y2 + y3;
            if (dyw > r || dyw < -r) continue;
            found.Add(i);
        }
    }
}
