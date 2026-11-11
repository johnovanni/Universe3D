using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Objects;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>
/// The Milky Way as a procedural barred spiral (the <c>shapes</c> impostor) centered at
/// <see cref="MilkyWayModel.CenterEq"/>, in the galactic plane, <see cref="MilkyWayModel.DiameterMpc"/> across,
/// plus the Sun marker at the origin.
/// </summary>
public sealed class MilkyWayRenderer : IDisposable
{
    /// <summary>The bar's near end leads the Sun–center line by about 27° toward positive galactic longitude.</summary>
    public const double BarAngleDeg = 27;

    private const float SunMarkerPx = 11;

    private readonly ShaderProgram _disk;
    private readonly ShaderProgram _marker;
    private readonly ShapeInstanceBuffer _buffer = new();
    private readonly int _markerVao;
    private readonly Vector3[] _naturalPalette = new Vector3[16];

    /// <summary>Requires a current GL context.</summary>
    public MilkyWayRenderer()
    {
        _disk = ShaderProgram.FromResources("shapes.vert", "shapes.frag");
        _marker = ShaderProgram.FromResources("marker.vert", "marker.frag");
        _markerVao = GL.GenVertexArray();
    }

    /// <summary>Shows the disk.</summary>
    public bool Visible { get; set; } = true;

    public bool SunMarkerVisible { get; set; } = true;

    /// <summary>The disk instance in a display frame: local x along the bar, local z along the galactic north pole.</summary>
    public static ShapeInstance Instance(DisplayFrame frame)
    {
        var toDisplay = SkyFrames.EquatorialTo(frame);
        var galToEq = SkyFrames.EquatorialToGalactic.Transpose();
        double bar = BarAngleDeg * Math.PI / 180;
        var x = toDisplay * (galToEq * new Vector3d(-Math.Cos(bar), Math.Sin(bar), 0));
        var z = toDisplay * MilkyWayModel.NormalEq;
        var y = z.Cross(x);
        var (high, low) = OrbitCamera.Split(toDisplay * MilkyWayModel.CenterEq);
        float r = (float)(MilkyWayModel.DiameterMpc / 2);
        return new ShapeInstance
        {
            PosHigh = high,
            PosLow = low,
            Rotation = ShapeRenderer.FromAxes(x, y, z),
            SemiAxesMpc = new Vector3(r, r, (float)ShapeRenderer.DiskThickness * r),
            Kind = ShapeKind.BarredSpiral,
            Cz = float.NaN,
        };
    }

    /// <summary>Places the disk for a display frame. Requires a current GL context.</summary>
    public void Upload(DisplayFrame frame) => _buffer.Upload(new[] { Instance(frame) }, 1);

    public void Render(OrbitCamera cam, Size viewport)
    {
        GL.Enable(EnableCap.Blend);
        if (Visible && _buffer.Count > 0)
        {
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            _disk.Use();
            ShapeRenderer.SetImpostorUniforms(_disk, cam, viewport);
            _disk.Set("u_fadeRange", new Vector2((float)ShapeRenderer.FadeStartPx, (float)ShapeRenderer.FadeEndPx));
            _disk.Set("u_maxDistance", float.MaxValue);
            _disk.Set("u_typeMask", uint.MaxValue);
            _disk.Set("u_colorMode", 2);
            _disk.Set("u_typeColors", _naturalPalette);
            _disk.Set("u_alpha", 1.0f);
            _buffer.Draw();
        }
        if (SunMarkerVisible)
        {
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.ProgramPointSize);
            _marker.Use();
            _marker.SetCamera(cam, viewport);
            _marker.Set("u_sizePx", SunMarkerPx);
            _marker.Set("u_color", new Vector3(1.0f, 0.86f, 0.3f));
            GL.BindVertexArray(_markerVao);
            GL.DrawArrays(PrimitiveType.Points, 0, 1);
            GL.BindVertexArray(0);
        }
    }

    public void Dispose()
    {
        _disk.Dispose();
        _marker.Dispose();
        _buffer.Dispose();
        GL.DeleteVertexArray(_markerVao);
    }
}
