using System.Windows;
using OpenTK.Graphics.OpenGL4;
using Universe3D.Core.Display;

namespace Universe3D.Rendering;

/// <summary>
/// Velocity arrows drawn by a geometry shader from the point cloud's vertex buffer, with the point cloud's
/// distance and type filters. Red = receding, blue = approaching.
/// </summary>
public sealed class VelocityArrowRenderer : IDisposable
{
    private readonly PointCloudRenderer _points;
    private readonly ShaderProgram _program;
    private readonly int _vao;

    /// <summary>Requires a current GL context.</summary>
    public VelocityArrowRenderer(PointCloudRenderer points)
    {
        _points = points;
        _program = ShaderProgram.FromResources("arrows.vert", "arrows.geom", "arrows.frag");
        _vao = points.Buffers.CreatePointVao();
    }

    public bool Visible { get; set; } = true;

    public ArrowMode Mode { get; set; } = ArrowMode.Observed;

    /// <summary>k in L = clamp(k·log10(1 + |v|/100), 3, 60) px.</summary>
    public double Scale { get; set; } = VelocityArrow.DefaultK;

    public void Render(OrbitCamera cam, Size viewport)
    {
        int count = _points.Buffers.PointCount;
        if (!Visible || count == 0) return;
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _program.Use();
        _program.SetCamera(cam, viewport);
        _program.Set("u_maxDistance",
            double.IsFinite(_points.MaxDistanceMpc) ? (float)_points.MaxDistanceMpc : float.MaxValue);
        _program.Set("u_typeMask", _points.TypeMask);
        _program.Set("u_mode", Mode == ArrowMode.PeculiarVelocity ? 1 : 0);
        _program.Set("u_k", (float)Scale);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, count);
        GL.BindVertexArray(0);
    }

    public void Dispose() => _program.Dispose();
}
