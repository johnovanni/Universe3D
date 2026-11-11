using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;

namespace Universe3D.Rendering;

/// <summary>
/// Galaxy clusters as instanced translucent spheres of radius R500 (1.5 Mpc when unknown), around the point
/// cloud's core points. Filters, color mode and palette follow the point cloud; a sphere fades in between 4 and
/// 8 px of projected diameter.
/// </summary>
public sealed class ClusterRenderer : IDisposable
{
    public const double DefaultRadiusMpc = 1.5;

    private readonly PointCloudRenderer _points;
    private readonly ShaderProgram _program;
    private readonly ShapeInstanceBuffer _buffer = new();

    /// <summary>Requires a current GL context.</summary>
    public ClusterRenderer(PointCloudRenderer points)
    {
        _points = points;
        _program = ShaderProgram.FromResources("shapes.vert", "clusters.frag");
    }

    public bool Visible { get; set; } = true;

    /// <summary>Spheres uploaded (0 unless a cluster data set is loaded).</summary>
    public int Count => _buffer.Count;

    public static double RadiusMpc(GalaxyCluster c) =>
        c.R500Mpc is double r && r > 0 && double.IsFinite(r) ? r : DefaultRadiusMpc;

    /// <summary>Builds one sphere per cluster in <paramref name="ds"/>. Requires a current GL context.</summary>
    public void Upload(DataSet ds, DisplayFrame frame)
    {
        var m = SkyFrames.EquatorialTo(frame);
        var clusters = ds.Objects.OfType<GalaxyCluster>().ToList();
        var instances = new ShapeInstance[clusters.Count];
        var identity = new Vector4(0, 0, 0, 1);
        for (int i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            var (high, low) = OrbitCamera.Split(m * c.PositionEq);
            float r = (float)RadiusMpc(c);
            instances[i] = new ShapeInstance
            {
                PosHigh = high,
                PosLow = low,
                Rotation = identity,
                SemiAxesMpc = new Vector3(r, r, r),
                Kind = ShapeKind.Sphere,
                TypeCode = c.TypeCode,
                Cz = (float)(VelocityArrow.ObservedVelocity(c.CzHelio, c.Z) ?? double.NaN),
                DistanceMpc = (float)c.DistanceMpc,
            };
        }
        _buffer.Upload(instances, instances.Length);
    }

    public void Render(OrbitCamera cam, Size viewport)
    {
        if (!Visible || _buffer.Count == 0) return;
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        _program.Use();
        ShapeRenderer.SetImpostorUniforms(_program, cam, viewport);
        _program.Set("u_fadeRange", new Vector2((float)ShapeRenderer.FadeStartPx, (float)ShapeRenderer.FadeEndPx));
        _program.Set("u_maxDistance",
            double.IsFinite(_points.MaxDistanceMpc) ? (float)_points.MaxDistanceMpc : float.MaxValue);
        _program.Set("u_typeMask", _points.TypeMask);
        _program.Set("u_colorMode", _points.ColorMode == ColorMode.Velocity ? 1 : 0);
        _program.Set("u_typeColors", _points.Palette);
        _program.Set("u_alpha", 1.0f);
        _buffer.Draw();
    }

    public void Dispose()
    {
        _program.Dispose();
        _buffer.Dispose();
    }
}
