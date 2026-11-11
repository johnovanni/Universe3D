using System.Windows;
using OpenTK.Mathematics;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>
/// Projects world points (Mpc, display frame) to screen pixels (origin top-left, y down) in double precision,
/// matching what the shaders draw for the same camera and viewport.
/// </summary>
public sealed class Projector
{
    private readonly Vector3d _eye;
    private readonly Vector3d _forward;
    private readonly double _width, _height, _focalPx;
    // Combined view-projection, row-vector convention: clip = (rel, 1) * M.
    private readonly double _m00, _m01, _m03, _m10, _m11, _m13, _m20, _m21, _m23, _m30, _m31, _m33;
    private readonly double _m02, _m12, _m22, _m32;

    public Projector(OrbitCamera cam, double widthPx, double heightPx)
    {
        _eye = cam.Eye;
        _forward = -cam.Backward;
        _width = widthPx;
        _height = heightPx;
        _focalPx = heightPx / (2 * Math.Tan(cam.FieldOfViewDeg * Math.PI / 360));
        Matrix4d m = cam.View * cam.Projection(widthPx / heightPx);
        (_m00, _m01, _m02, _m03) = (m.M11, m.M12, m.M13, m.M14);
        (_m10, _m11, _m12, _m13) = (m.M21, m.M22, m.M23, m.M24);
        (_m20, _m21, _m22, _m23) = (m.M31, m.M32, m.M33, m.M34);
        (_m30, _m31, _m32, _m33) = (m.M41, m.M42, m.M43, m.M44);
    }

    public double WidthPx => _width;
    public double HeightPx => _height;

    /// <summary>False when the point is behind the camera or outside the near/far planes.</summary>
    public bool TryProject(Vector3d world, out Point screen)
    {
        double x = world.X - _eye.X, y = world.Y - _eye.Y, z = world.Z - _eye.Z;
        double w = x * _m03 + y * _m13 + z * _m23 + _m33;
        if (w <= 0) { screen = default; return false; }
        double cz = x * _m02 + y * _m12 + z * _m22 + _m32;
        if (cz < -w || cz > w) { screen = default; return false; }
        double cx = x * _m00 + y * _m10 + z * _m20 + _m30;
        double cy = x * _m01 + y * _m11 + z * _m21 + _m31;
        screen = new Point((cx / w + 1) * 0.5 * _width, (1 - cy / w) * 0.5 * _height);
        return true;
    }

    /// <summary>
    /// Linear forms of (world − eye) for a click: <c>dx·w</c>, <c>dy·w</c> and <c>w</c>, where (dx, dy) is the
    /// screen offset from the click in pixels. Lets a caller reject far points without a division.
    /// </summary>
    internal ClickForms FormsFor(Point click)
    {
        double hw = _width / 2, hh = _height / 2;
        double ax = hw - click.X, ay = hh - click.Y;
        return new ClickForms(
            _eye,
            _m00 * hw + _m03 * ax, _m10 * hw + _m13 * ax, _m20 * hw + _m23 * ax, _m30 * hw + _m33 * ax,
            _m03 * ay - _m01 * hh, _m13 * ay - _m11 * hh, _m23 * ay - _m21 * hh, _m33 * ay - _m31 * hh,
            _m03, _m13, _m23, _m33);
    }

    internal readonly record struct ClickForms(
        Vector3d Eye,
        double Dx0, double Dx1, double Dx2, double Dx3,
        double Dy0, double Dy1, double Dy2, double Dy3,
        double W0, double W1, double W2, double W3);

    /// <summary>Pixels covered by a length (Mpc) seen face-on at a point; 0 when the point is behind the camera.</summary>
    public double PixelSize(double worldLength, Vector3d at)
    {
        double depth = (at - _eye).Dot(_forward);
        return depth <= 0 ? 0 : worldLength * _focalPx / depth;
    }
}
