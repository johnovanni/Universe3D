using OpenTK.Mathematics;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Rendering;

/// <summary>
/// A camera orbiting a target point, in Mpc in the display frame (z up). The view matrix is rotation only:
/// positions reach the GPU relative to the eye (see <see cref="Split"/>), so precision holds far from the Sun.
/// </summary>
public sealed class OrbitCamera
{
    public const double MinDistanceMpc = 1e-4;
    public const double MaxDistanceMpc = 1e5;
    public const double MaxPitchDeg = 89.9;
    public const double ZoomPerNotch = 1.15;
    public const double DegreesPerPixel = 0.25;

    private double _pitchDeg;
    private double _distanceMpc = 10;

    private Flight? _flight;

    private sealed record Flight(Vector3d FromTarget, double FromDistance, Vector3d ToTarget, double ToDistance,
                                 double Seconds)
    {
        public double Elapsed { get; set; }
    }

    public Vector3d Target { get; set; }

    public double YawDeg { get; set; }

    public double PitchDeg
    {
        get => _pitchDeg;
        set => _pitchDeg = Math.Clamp(value, -MaxPitchDeg, MaxPitchDeg);
    }

    public double DistanceMpc
    {
        get => _distanceMpc;
        set => _distanceMpc = Math.Clamp(value, MinDistanceMpc, MaxDistanceMpc);
    }

    public double FieldOfViewDeg { get; set; } = 45;

    public bool IsFlying => _flight != null;

    /// <summary>Positive notches move closer: the distance is divided by 1.15 per notch.</summary>
    public void Zoom(int notches)
    {
        _flight = null;
        DistanceMpc /= Math.Pow(ZoomPerNotch, notches);
    }

    public void Rotate(double dxPx, double dyPx)
    {
        YawDeg = NormalizeDeg(YawDeg - dxPx * DegreesPerPixel);
        PitchDeg += dyPx * DegreesPerPixel;
    }

    /// <summary>Moves the target so the scene follows the mouse at the target's depth.</summary>
    public void Pan(double dxPx, double dyPx, double viewportHeightPx)
    {
        if (viewportHeightPx <= 0) return;
        _flight = null;
        double mpcPerPx = 2 * DistanceMpc * Math.Tan(FovRad / 2) / viewportHeightPx;
        var (right, up) = ScreenAxes();
        Target = Target - right * (dxPx * mpcPerPx) + up * (dyPx * mpcPerPx);
    }

    public void FlyTo(Vector3d target, double distanceMpc, double seconds = 0.8)
    {
        double to = Math.Clamp(distanceMpc, MinDistanceMpc, MaxDistanceMpc);
        if (seconds <= 0)
        {
            _flight = null;
            Target = target;
            DistanceMpc = to;
            return;
        }
        _flight = new Flight(Target, DistanceMpc, target, to, seconds);
    }

    /// <summary>Advances a fly-to, eased with smoothstep; distance is interpolated geometrically.</summary>
    public void Update(double dtSeconds)
    {
        if (_flight is not { } f) return;
        f.Elapsed += dtSeconds;
        if (f.Elapsed >= f.Seconds - 1e-9)
        {
            Target = f.ToTarget;
            DistanceMpc = f.ToDistance;
            _flight = null;
            return;
        }
        double t = f.Elapsed / f.Seconds;
        double s = t * t * (3 - 2 * t);
        Target = f.FromTarget + (f.ToTarget - f.FromTarget) * s;
        DistanceMpc = Math.Exp(Math.Log(f.FromDistance) + (Math.Log(f.ToDistance) - Math.Log(f.FromDistance)) * s);
    }

    /// <summary>Unit vector from the target toward the eye.</summary>
    public Vector3d Backward
    {
        get
        {
            double yaw = YawDeg * Math.PI / 180, pitch = PitchDeg * Math.PI / 180;
            return new Vector3d(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        }
    }

    public Vector3d Eye => Target + Backward * DistanceMpc;

    /// <summary>World-to-view rotation (no translation), for row vectors as OpenTK uses.</summary>
    public Matrix4d View
    {
        get
        {
            var b = Backward;
            var forward = new OpenTK.Mathematics.Vector3d(-b.X, -b.Y, -b.Z);
            return Matrix4d.LookAt(OpenTK.Mathematics.Vector3d.Zero, forward, OpenTK.Mathematics.Vector3d.UnitZ);
        }
    }

    public double NearMpc => DistanceMpc * 1e-3;

    public double FarMpc => DistanceMpc + 2 * MaxDistanceMpc;

    public Matrix4d Projection(double aspect) =>
        Matrix4d.CreatePerspectiveFieldOfView(FovRad, aspect > 0 ? aspect : 1, NearMpc, FarMpc);

    /// <summary>
    /// Relative-to-eye encoding: <c>high</c> is v rounded to float, <c>low</c> the float of the remainder,
    /// so <c>(p.high − eye.high) + (p.low − eye.low)</c> keeps about 1e-12 relative precision on the GPU.
    /// </summary>
    public static (Vector3 high, Vector3 low) Split(Vector3d v)
    {
        var high = new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        var low = new Vector3((float)(v.X - high.X), (float)(v.Y - high.Y), (float)(v.Z - high.Z));
        return (high, low);
    }

    private double FovRad => FieldOfViewDeg * Math.PI / 180;

    private (Vector3d right, Vector3d up) ScreenAxes()
    {
        var forward = -Backward;
        var right = forward.Cross(new Vector3d(0, 0, 1)).Normalized();
        var up = right.Cross(forward);
        return (right, up);
    }

    private static double NormalizeDeg(double deg)
    {
        deg %= 360;
        return deg < 0 ? deg + 360 : deg;
    }
}
