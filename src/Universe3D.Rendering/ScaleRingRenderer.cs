using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Universe3D.Core.Display;
using Universe3D.Core.Text;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>A ring label: its text and where to draw it, in framebuffer pixels (origin top-left).</summary>
public sealed record RingLabel(string Text, Point ScreenPx);

/// <summary>
/// Scale rings around the Sun in the display frame's reference plane (z = 0), at the radii from
/// <see cref="ScaleRings.RadiiLy"/> for the camera distance. Labels are positioned for an overlay.
/// </summary>
public sealed class ScaleRingRenderer : IDisposable
{
    private const int Segments = 256;
    private const int LabelSamples = 96;
    private const double LabelMarginPx = 24;

    private readonly ShaderProgram _program;
    private readonly int _vao, _vbo;
    private readonly List<RingLabel> _labels = new();

    /// <summary>Requires a current GL context.</summary>
    public ScaleRingRenderer()
    {
        _program = ShaderProgram.FromResources("rings.vert", "rings.frag");
        var unit = new float[2 * Segments];
        for (int i = 0; i < Segments; i++)
        {
            double a = 2 * Math.PI * i / Segments;
            unit[2 * i] = (float)Math.Cos(a);
            unit[2 * i + 1] = (float)Math.Sin(a);
        }
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, unit.Length * sizeof(float), unit, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public bool Visible { get; set; } = true;

    /// <summary>Labels from the last frame, in framebuffer pixels.</summary>
    public IReadOnlyList<RingLabel> Labels => _labels;

    public void Render(OrbitCamera cam, Size viewport)
    {
        _labels.Clear();
        if (!Visible || viewport.Width <= 0 || viewport.Height <= 0) return;
        var radiiLy = ScaleRings.RadiiLy(cam.DistanceMpc * Units.LyPerMpc);
        if (radiiLy.Count == 0) return;

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _program.Use();
        _program.SetCamera(cam, viewport);
        _program.Set("u_color", new Vector4(0.45f, 0.62f, 0.85f, 0.45f));
        var proj = new Projector(cam, viewport.Width, viewport.Height);
        GL.BindVertexArray(_vao);
        foreach (double ly in radiiLy)
        {
            double mpc = ly / Units.LyPerMpc;
            _program.Set("u_radius", (float)mpc);
            GL.DrawArrays(PrimitiveType.LineLoop, 0, Segments);
            if (LabelPoint(proj, mpc) is { } at) _labels.Add(new RingLabel(ScaleRings.Label(ly), at));
        }
        GL.BindVertexArray(0);
    }

    /// <summary>The lowest point of the ring on screen that keeps a margin from the edges, or null if none.</summary>
    internal static Point? LabelPoint(Projector proj, double radiusMpc)
    {
        Point? best = null;
        for (int i = 0; i < LabelSamples; i++)
        {
            double a = 2 * Math.PI * i / LabelSamples;
            var p = new Vector3d(radiusMpc * Math.Cos(a), radiusMpc * Math.Sin(a), 0);
            if (!proj.TryProject(p, out var s)) continue;
            if (s.X < LabelMarginPx || s.X > proj.WidthPx - LabelMarginPx ||
                s.Y < LabelMarginPx || s.Y > proj.HeightPx - LabelMarginPx) continue;
            if (best is not { } b || s.Y > b.Y) best = s;
        }
        return best;
    }

    public void Dispose()
    {
        _program.Dispose();
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
    }
}
