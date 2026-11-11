using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

public enum ColorMode { Type, Velocity }

/// <summary>Draws a data set as soft point sprites, filtered by distance and type bits on the GPU.</summary>
public sealed class PointCloudRenderer : IDisposable
{
    private readonly ShaderProgram _program;
    private readonly int _vao;
    private Vector2 _magRange;
    private Vector3[] _palette = TypePalette(DataSetKind.LocalVolume);
    private PointVertex[] _vertices = Array.Empty<PointVertex>();

    /// <summary>Requires a current GL context.</summary>
    public PointCloudRenderer(GpuBuffers buffers)
    {
        Buffers = buffers;
        _program = ShaderProgram.FromResources("points.vert", "points.frag");
        _vao = buffers.CreatePointVao();
    }

    public GpuBuffers Buffers { get; }

    public DataSetKind? Kind { get; private set; }

    /// <summary>Positions (Mpc) in the display frame, by object index; the input to <see cref="Picker"/>.</summary>
    public Vector3d[] Positions { get; private set; } = Array.Empty<Vector3d>();

    public double MaxDistanceMpc { get; set; } = double.PositiveInfinity;

    /// <summary>Bit <c>TypeCode</c> set = shown.</summary>
    public uint TypeMask { get; set; } = uint.MaxValue;

    public ColorMode ColorMode { get; set; } = ColorMode.Type;

    public double PointScale { get; set; } = 1.0;

    /// <summary>CPU mirror of the shader filter, for picking.</summary>
    public bool IsVisible(int index)
    {
        if ((uint)index >= (uint)_vertices.Length) return false;
        ref readonly var v = ref _vertices[index];
        return v.DistanceMpc <= MaxDistanceMpc && ((TypeMask >> (int)Math.Min(v.TypeCode, 31u)) & 1u) != 0;
    }

    /// <summary>Builds and uploads the vertex data. Requires a current GL context.</summary>
    public void Upload(DataSet ds, DisplayFrame frame)
    {
        var m = SkyFrames.EquatorialTo(frame);
        int n = ds.Objects.Count;
        var vertices = new PointVertex[n];
        var positions = new Vector3d[n];
        var mags = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            var o = ds.Objects[i];
            var p = m * o.PositionEq;
            positions[i] = p;
            var (high, low) = OrbitCamera.Split(p);
            vertices[i] = new PointVertex
            {
                PosHigh = high,
                PosLow = low,
                DistanceMpc = (float)o.DistanceMpc,
                TypeCode = o.TypeCode,
                Cz = (float)(VelocityArrow.ObservedVelocity(o.CzHelio, o.Z) ?? double.NaN),
                Pec = (float)(o.PeculiarKms ?? double.NaN),
                Magnitude = (float)(o.Magnitude ?? double.NaN),
                DiameterMpc = (float)DiameterMpc(o),
                Index = (uint)i,
            };
            if (o.Magnitude is double mag && double.IsFinite(mag)) mags.Add(mag);
        }

        Buffers.UploadPoints(vertices);
        _vertices = vertices;
        Positions = positions;
        Kind = ds.Kind;
        _palette = TypePalette(ds.Kind);
        _magRange = MagnitudeRange(mags);
    }

    public void Render(OrbitCamera cam, Size viewport)
    {
        if (Buffers.PointCount == 0 || Kind is not { } kind) return;
        bool additive = kind == DataSetKind.Quasars;
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, additive ? BlendingFactor.One : BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.ProgramPointSize);

        _program.Use();
        _program.SetCamera(cam, viewport);
        _program.Set("u_maxDistance", double.IsFinite(MaxDistanceMpc) ? (float)MaxDistanceMpc : float.MaxValue);
        _program.Set("u_typeMask", TypeMask);
        _program.Set("u_colorMode", ColorMode == ColorMode.Velocity ? 1 : 0);
        _program.Set("u_typeColors", _palette);
        _program.Set("u_magRange", _magRange);
        _program.Set("u_pointScale", (float)PointScale);
        _program.Set("u_alpha", additive ? 0.55f : 0.9f);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, Buffers.PointCount);
        GL.BindVertexArray(0);
    }

    private static double DiameterMpc(CosmicObject o) => o switch
    {
        Galaxy { DiameterKpc: double kpc } => kpc / 1000.0,
        GalaxyCluster { R500Mpc: double r } => 2 * r,
        _ => 0,
    };

    /// <summary>5th to 95th percentile, bright to faint; (0, 0) disables magnitude sizing.</summary>
    private static Vector2 MagnitudeRange(List<double> mags)
    {
        if (mags.Count < 2) return Vector2.Zero;
        mags.Sort();
        double bright = mags[(int)(0.05 * (mags.Count - 1))], faint = mags[(int)(0.95 * (mags.Count - 1))];
        return faint > bright ? new Vector2((float)bright, (float)faint) : Vector2.Zero;
    }

    /// <summary>Colors by <c>TypeCode</c>: morphology class, cluster catalog bits (R7) or quasar type.</summary>
    private static Vector3[] TypePalette(DataSetKind kind)
    {
        var p = new Vector3[16];
        Array.Fill(p, new Vector3(0.7f, 0.7f, 0.7f));
        switch (kind)
        {
            case DataSetKind.LocalVolume:
            case DataSetKind.NearbyUniverse:
                p[0] = new(0.62f, 0.62f, 0.62f); // Unknown
                p[1] = new(1.00f, 0.72f, 0.35f); // Elliptical
                p[2] = new(1.00f, 0.90f, 0.55f); // Lenticular
                p[3] = new(0.55f, 0.75f, 1.00f); // Spiral
                p[4] = new(0.45f, 0.95f, 1.00f); // Barred spiral
                p[5] = new(0.55f, 1.00f, 0.55f); // Irregular
                p[6] = new(1.00f, 0.70f, 0.85f); // Dwarf spheroidal
                p[7] = new(0.70f, 1.00f, 0.75f); // Dwarf irregular
                p[8] = new(1.00f, 0.45f, 1.00f); // Peculiar
                break;
            case DataSetKind.Clusters:
                p[1] = new(0.75f, 0.55f, 1.00f); // MCXC only
                p[2] = new(1.00f, 0.65f, 0.30f); // Abell only
                p[3] = new(1.00f, 1.00f, 1.00f); // both
                break;
            case DataSetKind.Quasars:
                p[0] = new(0.60f, 0.90f, 1.00f); // Quasar
                p[1] = new(1.00f, 0.85f, 0.40f); // AGN
                p[2] = new(1.00f, 0.45f, 0.95f); // BL Lac
                p[3] = new(0.55f, 1.00f, 0.55f); // Type 2
                break;
        }
        return p;
    }

    public void Dispose() => _program.Dispose();
}
