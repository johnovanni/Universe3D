using System.Runtime.InteropServices;
using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using Universe3D.Core.Shapes;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;
using Matrix3d = Universe3D.Core.Coordinates.Matrix3d;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>
/// The CPU side of the shape level of detail: galaxy positions (display frame), diameters, distances and type
/// codes, prepared once per data set so a selection is a single pass plus a partial sort.
/// </summary>
public sealed class ShapeCandidates
{
    private readonly Vector3d[] _pos;
    private readonly double[] _diameter;
    private readonly double[] _distance;
    private readonly uint[] _type;
    private readonly int[] _galaxyIndex;
    private readonly double[] _negPx;
    private readonly int[] _order;
    private readonly int[] _result;

    /// <summary>Galaxies without a positive diameter are left out (they stay points).</summary>
    public ShapeCandidates(IReadOnlyList<Galaxy> galaxies, DisplayFrame frame)
    {
        var m = SkyFrames.EquatorialTo(frame);
        var keep = new List<int>(galaxies.Count);
        for (int i = 0; i < galaxies.Count; i++)
            if (galaxies[i].DiameterKpc is double kpc && kpc > 0 && double.IsFinite(kpc)) keep.Add(i);
        int n = keep.Count;
        _pos = new Vector3d[n];
        _diameter = new double[n];
        _distance = new double[n];
        _type = new uint[n];
        _galaxyIndex = keep.ToArray();
        for (int k = 0; k < n; k++)
        {
            var g = galaxies[keep[k]];
            _pos[k] = m * g.PositionEq;
            _diameter[k] = g.DiameterKpc!.Value / 1000.0;
            _distance[k] = g.DistanceMpc;
            _type[k] = g.TypeCode;
        }
        _negPx = new double[n];
        _order = new int[n];
        _result = new int[n];
    }

    /// <summary>Galaxies that can become shapes.</summary>
    public int Count => _pos.Length;

    /// <inheritdoc cref="Select(Projector, double, uint, int, out double)"/>
    public IReadOnlyList<int> Select(Projector proj, double maxDistanceMpc, uint typeMask, int cap) =>
        Select(proj, maxDistanceMpc, typeMask, cap, out _);

    /// <summary>
    /// Indices (into the galaxy list given to the constructor) of the galaxies shown, on screen, and wider than
    /// <see cref="ShapeRenderer.FadeStartPx"/>, largest first, at most <paramref name="cap"/>. The list is reused by
    /// the next call. <paramref name="cutoffPx"/> is the smallest selected size when the cap left galaxies out, else 0.
    /// </summary>
    public IReadOnlyList<int> Select(Projector proj, double maxDistanceMpc, uint typeMask, int cap, out double cutoffPx)
    {
        // Plain doubles in the loop (as in Picker): Vector3d lives in Core, which Debug builds don't optimize.
        var eye = proj.Eye;
        var fwd = proj.Forward;
        double ex = eye.X, ey = eye.Y, ez = eye.Z, fx = fwd.X, fy = fwd.Y, fz = fwd.Z, focal = proj.FocalPx;
        double w = proj.WidthPx, h = proj.HeightPx;
        ReadOnlySpan<double> xyz = MemoryMarshal.Cast<Vector3d, double>(_pos.AsSpan());
        int n = 0;
        for (int k = 0; k < _pos.Length; k++)
        {
            if (!(_distance[k] <= maxDistanceMpc) || ((typeMask >> (int)Math.Min(_type[k], 31u)) & 1u) == 0) continue;
            double dx = xyz[3 * k] - ex, dy = xyz[3 * k + 1] - ey, dz = xyz[3 * k + 2] - ez;
            double diameter = _diameter[k];
            double px;
            if (dx * dx + dy * dy + dz * dz < diameter * diameter / 4)
            {
                px = double.MaxValue; // the camera is inside it
            }
            else
            {
                double depth = dx * fx + dy * fy + dz * fz; // as Projector.PixelSize
                if (depth <= 0) continue;
                px = diameter * focal / depth;
                if (!(px > ShapeRenderer.FadeStartPx)) continue;
                // On screen, with a margin of the whole projected diameter.
                if (!proj.TryProject(_pos[k], out var s) || s.X < -px || s.X > w + px || s.Y < -px || s.Y > h + px) continue;
            }
            _negPx[n] = -px;
            _order[n] = k;
            n++;
        }

        int count = Math.Min(n, Math.Max(cap, 0));
        if (count < n) SmallestFirst(_negPx, _order, n, count);
        Array.Sort(_negPx, _order, 0, count);
        for (int i = 0; i < count; i++) _result[i] = _galaxyIndex[_order[i]];
        cutoffPx = count < n && count > 0 ? -_negPx[count - 1] : 0;
        return new ArraySegment<int>(_result, 0, count);
    }

    /// <summary>Quickselect: moves the <paramref name="k"/> smallest keys (with their items) to the front, unordered.</summary>
    private static void SmallestFirst(double[] keys, int[] items, int n, int k)
    {
        int lo = 0, hi = n - 1;
        while (lo < hi)
        {
            double pivot = keys[(lo + hi) >>> 1];
            int i = lo, j = hi;
            while (i <= j)
            {
                while (keys[i] < pivot) i++;
                while (keys[j] > pivot) j--;
                if (i <= j)
                {
                    (keys[i], keys[j]) = (keys[j], keys[i]);
                    (items[i], items[j]) = (items[j], items[i]);
                    i++;
                    j--;
                }
            }
            if (k - 1 <= j) hi = j;
            else if (k - 1 >= i) lo = i;
            else return;
        }
    }
}

/// <summary>
/// The shape level of detail without GL: which galaxies are shapes for the current camera and filters (re-selected
/// only when they change), and the cross-fade window the point shader uses.
/// </summary>
public sealed class ShapeLod
{
    private readonly ShapeCandidates? _candidates;
    private IReadOnlyList<int> _selected = Array.Empty<int>();
    private SelectionKey? _selectedFor;

    private readonly record struct SelectionKey(
        Vector3d Eye, double Yaw, double Pitch, double Fov, double W, double H, double MaxDistance, uint TypeMask,
        int Cap);

    /// <param name="candidates">The loaded data set's galaxies, or null for none.</param>
    public ShapeLod(ShapeCandidates? candidates) => _candidates = candidates;

    /// <summary>Indices into the candidates' galaxy list, largest first; empty while shapes are hidden.</summary>
    public IReadOnlyList<int> Selected => _selected;

    /// <summary>
    /// The cross-fade window in framebuffer pixels: (4, 8), or shifted up to the smallest shape drawn when the cap
    /// left galaxies out (so those stay points). (0, 0) when no shapes can be drawn, which turns the point fade off.
    /// </summary>
    public Vector2 FadeRangePx { get; private set; }

    /// <summary>
    /// Re-selects when the camera, viewport (framebuffer px), filters or cap changed, or the shapes were hidden since
    /// the last call. Returns true when <see cref="Selected"/> changed.
    /// </summary>
    public bool Update(bool visible, OrbitCamera cam, double widthPx, double heightPx, double maxDistanceMpc,
                       uint typeMask, int cap)
    {
        if (!visible || _candidates == null || _candidates.Count == 0 || widthPx <= 0 || heightPx <= 0)
        {
            bool changed = _selected.Count > 0;
            _selected = Array.Empty<int>();
            _selectedFor = null; // the next visible call re-selects, even with the camera unchanged
            FadeRangePx = Vector2.Zero;
            return changed;
        }
        var key = new SelectionKey(cam.Eye, cam.YawDeg, cam.PitchDeg, cam.FieldOfViewDeg, widthPx, heightPx,
                                   maxDistanceMpc, typeMask, cap);
        if (key == _selectedFor) return false;
        _selectedFor = key;
        _selected = _candidates.Select(new Projector(cam, widthPx, heightPx), maxDistanceMpc, typeMask, cap,
                                       out double cutoff);
        FadeRangePx = FadeWindow(cutoff);
        return true;
    }

    /// <summary>(lo, 2·lo) with lo = max(4, cutoff): the spec's (4, 8), shifted up when the cap binds.</summary>
    public static Vector2 FadeWindow(double cutoffPx)
    {
        float lo = (float)Math.Clamp(cutoffPx, ShapeRenderer.FadeStartPx, 1e6);
        return new Vector2(lo, lo * (float)(ShapeRenderer.FadeEndPx / ShapeRenderer.FadeStartPx));
    }
}

/// <summary>
/// Galaxy shapes: instanced ellipsoid impostors for galaxies whose projected diameter exceeds 4 px, chosen on the
/// CPU when the camera or the filters change. Disks (spirals, barred spirals, lenticulars, irregulars and
/// unclassified galaxies) are oblate spheroids of thickness q₀ = 0.2 oriented by
/// <see cref="GalaxyOrientation.DiskRotation"/>; ellipticals and dwarf spheroidals are ellipsoids with axis ratio q,
/// their long axis along the PA on the sky. The shape fades in as <c>smoothstep(4, 8, px)</c> while the point fades out.
/// </summary>
public sealed class ShapeRenderer : IDisposable
{
    public const double FadeStartPx = 4, FadeEndPx = 8;
    public const int RecommendedCap = 20_000, MinimumCap = 5_000;

    /// <summary>Intrinsic disk thickness q₀ (global constraint).</summary>
    public const double DiskThickness = 0.2;

    /// <summary>Axis ratio for ellipticals and dwarf spheroidals without one.</summary>
    private const double DefaultEllipsoidAxisRatio = 0.8;

    private readonly PointCloudRenderer _points;
    private readonly ShaderProgram _program;
    private readonly ShapeInstanceBuffer _buffer = new();

    private ShapeLod _lod = new(null);
    private ShapeInstance[] _byGalaxy = Array.Empty<ShapeInstance>();
    private ShapeInstance[] _staging = Array.Empty<ShapeInstance>();

    /// <summary>Requires a current GL context. Filters, color mode and palette follow <paramref name="points"/>.</summary>
    public ShapeRenderer(PointCloudRenderer points)
    {
        _points = points;
        _program = ShaderProgram.FromResources("shapes.vert", "shapes.frag");
    }

    public bool Visible { get; set; } = true;

    /// <summary>At most this many shapes are drawn (20,000 on recommended GPUs, 5,000 on minimum ones).</summary>
    public int Cap { get; set; } = RecommendedCap;

    /// <summary>Shapes drawn in the last frame.</summary>
    public int ShapeCount => Visible ? _buffer.Count : 0;

    /// <inheritdoc cref="ShapeLod.FadeRangePx"/>
    public Vector2 FadeRangePx => _lod.FadeRangePx;

    /// <summary>
    /// Indices into <paramref name="galaxies"/> of the shapes to draw: shown by the distance and type filters, on
    /// screen, projected diameter &gt; 4 px, largest first, at most <paramref name="cap"/>. Positions are projected in
    /// <paramref name="frame"/>, the frame <paramref name="proj"/> was built for.
    /// </summary>
    public static IReadOnlyList<int> SelectInstances(IReadOnlyList<Galaxy> galaxies, Projector proj,
                                                     double maxDistanceMpc, uint typeMask, int cap,
                                                     DisplayFrame frame = DisplayFrame.Equatorial) =>
        new ShapeCandidates(galaxies, frame).Select(proj, maxDistanceMpc, typeMask, cap).ToArray();

    /// <summary>Builds the per-galaxy instances. Requires a current GL context.</summary>
    public void Upload(DataSet ds, DisplayFrame frame)
    {
        var galaxies = ds.Objects.OfType<Galaxy>().ToList();
        var m = SkyFrames.EquatorialTo(frame);
        var candidates = new ShapeCandidates(galaxies, frame);
        _lod = new ShapeLod(candidates);
        _byGalaxy = new ShapeInstance[galaxies.Count];
        for (int i = 0; i < galaxies.Count; i++)
            if (galaxies[i].DiameterKpc is double kpc && kpc > 0) _byGalaxy[i] = Build(galaxies[i], m);
        _staging = new ShapeInstance[Math.Min(candidates.Count, 1 << 20)];
        _buffer.Upload(_staging, 0);
    }

    /// <summary>Re-selects the instances when the camera, viewport (framebuffer px), filters or visibility changed.</summary>
    public void Update(OrbitCamera cam, Size viewport)
    {
        if (!_lod.Update(Visible, cam, viewport.Width, viewport.Height, _points.MaxDistanceMpc, _points.TypeMask, Cap))
            return;
        var selected = _lod.Selected;
        int count = Math.Min(selected.Count, _staging.Length);
        for (int i = 0; i < count; i++) _staging[i] = _byGalaxy[selected[i]];
        _buffer.Upload(_staging, count);
    }

    public void Render(OrbitCamera cam, Size viewport)
    {
        // A (0, 0) window means no selection is current: draw nothing rather than shapes without a fade.
        if (!Visible || _buffer.Count == 0 || FadeRangePx.Y <= FadeRangePx.X) return;
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        _program.Use();
        SetImpostorUniforms(_program, cam, viewport);
        _program.Set("u_fadeRange", FadeRangePx);
        // The CPU selection already applied the filters.
        _program.Set("u_maxDistance", float.MaxValue);
        _program.Set("u_typeMask", uint.MaxValue);
        _program.Set("u_colorMode", _points.ColorMode == ColorMode.Velocity ? 1 : 0);
        _program.Set("u_typeColors", _points.Palette);
        _program.Set("u_alpha", 1.0f);
        _buffer.Draw();
    }

    /// <summary>Camera uniforms for <c>shapes.vert</c>: the shared ones plus the screen axes, near plane and field of view.</summary>
    internal static void SetImpostorUniforms(ShaderProgram program, OrbitCamera cam, Size viewport)
    {
        program.SetCamera(cam, viewport);
        var (right, up) = cam.ScreenAxes();
        program.Set("u_forward", ToVector3(-cam.Backward));
        program.Set("u_right", ToVector3(right));
        program.Set("u_up", ToVector3(up));
        program.Set("u_near", (float)cam.NearMpc);
        program.Set("u_tanHalfFov", (float)Math.Tan(cam.FieldOfViewDeg * Math.PI / 360));
        program.Set("u_aspect", (float)(viewport.Height > 0 ? viewport.Width / viewport.Height : 1));
    }

    internal static Vector3 ToVector3(Vector3d v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>The instance for one galaxy (which must have a diameter) in a display frame.</summary>
    public static ShapeInstance Instance(Galaxy g, DisplayFrame frame) => Build(g, SkyFrames.EquatorialTo(frame));

    private static ShapeInstance Build(Galaxy g, Matrix3d toDisplay)
    {
        double r = g.DiameterKpc!.Value / 2000.0;
        // Missing PA or inclination: a fixed pseudo-random value per galaxy (cos i uniform), so it never changes.
        double pa = g.PaDeg ?? 180 * Hash01(g.Id, 1);
        var sun = SkyMath.UnitVector(g.RaDeg, g.DecDeg);
        var kind = KindOf(g.Morphology.Class);
        NQuaternion rotation;
        Vector3d axes;
        if (kind == ShapeKind.Ellipsoid)
        {
            // Seen from the Sun along local z: long axis x at the PA, short axis y = q·x.
            double q = Math.Clamp(g.AxisRatio ?? DefaultEllipsoidAxisRatio, 0.1, 1);
            rotation = GalaxyOrientation.DiskRotation(-sun, pa, g.RaDeg, g.DecDeg);
            axes = new Vector3d(r, q * r, q * r);
        }
        else
        {
            double incl = g.InclinationDeg
                          ?? (g.AxisRatio is double q ? GalaxyOrientation.InclinationDeg(q, DiskThickness) : (double?)null)
                          ?? Math.Acos(Hash01(g.Id, 2)) * 180 / Math.PI;
            var normal = GalaxyOrientation.DiskNormal(g.RaDeg, g.DecDeg, pa, incl);
            rotation = GalaxyOrientation.DiskRotation(normal, pa, g.RaDeg, g.DecDeg);
            axes = new Vector3d(r, r, DiskThickness * r);
        }

        var (high, low) = OrbitCamera.Split(toDisplay * g.PositionEq);
        return new ShapeInstance
        {
            PosHigh = high,
            PosLow = low,
            Rotation = ToDisplay(rotation, toDisplay),
            SemiAxesMpc = ToVector3(axes),
            Kind = kind,
            TypeCode = g.TypeCode,
            Cz = (float)(Core.Display.VelocityArrow.ObservedVelocity(g.CzHelio, g.Z) ?? double.NaN),
            Seed = (float)Hash01(g.Id, 0),
            DistanceMpc = (float)g.DistanceMpc,
        };
    }

    /// <summary>R19: unclassified galaxies are drawn as smooth lenticular-like disks.</summary>
    internal static ShapeKind KindOf(MorphologyClass c) => c switch
    {
        MorphologyClass.Elliptical or MorphologyClass.DwarfSpheroidal => ShapeKind.Ellipsoid,
        MorphologyClass.Spiral => ShapeKind.Spiral,
        MorphologyClass.BarredSpiral => ShapeKind.BarredSpiral,
        MorphologyClass.Irregular or MorphologyClass.DwarfIrregular or MorphologyClass.Peculiar => ShapeKind.Irregular,
        _ => ShapeKind.Lenticular,
    };

    /// <summary>An equatorial orientation re-expressed in the display frame, as (x, y, z, w).</summary>
    internal static Vector4 ToDisplay(NQuaternion equatorial, Matrix3d toDisplay)
    {
        Vector3d Axis(NVector3 local)
        {
            var a = NVector3.Transform(local, equatorial);
            return toDisplay * new Vector3d(a.X, a.Y, a.Z);
        }
        return FromAxes(Axis(NVector3.UnitX), Axis(NVector3.UnitY), Axis(NVector3.UnitZ));
    }

    /// <summary>The rotation taking local x, y, z to the given orthonormal axes, as (x, y, z, w).</summary>
    internal static Vector4 FromAxes(Vector3d x, Vector3d y, Vector3d z)
    {
        // System.Numerics uses row vectors: row k is the image of local axis k.
        var m = new System.Numerics.Matrix4x4(
            (float)x.X, (float)x.Y, (float)x.Z, 0,
            (float)y.X, (float)y.Y, (float)y.Z, 0,
            (float)z.X, (float)z.Y, (float)z.Z, 0,
            0, 0, 0, 1);
        var q = NQuaternion.Normalize(NQuaternion.CreateFromRotationMatrix(m));
        return new Vector4(q.X, q.Y, q.Z, q.W);
    }

    /// <summary>A stable value in [0, 1) from an object id (FNV-1a; string.GetHashCode varies between runs).</summary>
    internal static double Hash01(string id, int salt)
    {
        uint h = 2166136261u ^ (uint)salt * 16777619u;
        foreach (char c in id)
        {
            h ^= c;
            h *= 16777619u;
        }
        h ^= h >> 15;
        h *= 0x2c1b3c6du;
        h ^= h >> 12;
        return h / 4294967296.0;
    }

    public void Dispose()
    {
        _program.Dispose();
        _buffer.Dispose();
    }
}
