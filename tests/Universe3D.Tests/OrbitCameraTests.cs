using System.Windows;
using OpenTK.Mathematics;
using Universe3D.Rendering;
using Vector3d = Universe3D.Core.Coordinates.Vector3d;

namespace Universe3D.Tests;

public class OrbitCameraTests
{
    [Fact]
    public void Camera_ZoomFifteenPercent()
    {
        var cam = new OrbitCamera { DistanceMpc = 10 };
        cam.Zoom(1);
        Assert.InRange(cam.DistanceMpc, 8.6957 - 1e-4, 8.6957 + 1e-4);

        for (int i = 0; i < 200; i++)
        {
            cam.Zoom(1);
            Assert.InRange(cam.DistanceMpc, 1e-4, 1e5);
        }
        Assert.Equal(1e-4, cam.DistanceMpc);

        cam.Zoom(-400);
        Assert.Equal(1e5, cam.DistanceMpc);
        for (int i = 0; i < 50; i++)
        {
            cam.Zoom(-1);
            Assert.InRange(cam.DistanceMpc, 1e-4, 1e5);
        }
    }

    [Fact]
    public void Camera_PitchClamped()
    {
        var cam = new OrbitCamera { DistanceMpc = 10 };
        cam.Rotate(0, 1e6);
        Assert.True(Math.Abs(cam.PitchDeg) <= 89.9, $"pitch {cam.PitchDeg}");
        cam.Rotate(0, -1e7);
        Assert.True(Math.Abs(cam.PitchDeg) <= 89.9, $"pitch {cam.PitchDeg}");
        cam.PitchDeg = 1000;
        Assert.Equal(89.9, cam.PitchDeg);
    }

    [Fact]
    public void Camera_FlyToEndsAtTarget()
    {
        var cam = new OrbitCamera { Target = new Vector3d(0, 0, 0), DistanceMpc = 10 };
        var end = new Vector3d(100.123, 50.456, -20.789);
        cam.FlyTo(end, 2.0);
        Assert.True(cam.IsFlying);

        for (int i = 0; i < 4; i++) cam.Update(0.1);
        Assert.True(cam.DistanceMpc < 10 && cam.DistanceMpc > 2.0, $"halfway distance {cam.DistanceMpc}");
        Assert.True(cam.Target.X > 0 && cam.Target.X < end.X, $"halfway target {cam.Target}");

        for (int i = 0; i < 4; i++) cam.Update(0.1);
        Assert.Equal(end, cam.Target);
        Assert.Equal(2.0, cam.DistanceMpc);
        Assert.False(cam.IsFlying);
    }

    [Fact]
    public void OrbitCamera_RelativeToEyeSplitKeepsPrecision()
    {
        var v = new Vector3d(200.000123456, -150.000987654, 75.123456789);
        var (high, low) = OrbitCamera.Split(v);
        Assert.True(Math.Abs((double)high.X + low.X - v.X) < 1e-9);
        Assert.True(Math.Abs((double)high.Y + low.Y - v.Y) < 1e-9);
        Assert.True(Math.Abs((double)high.Z + low.Z - v.Z) < 1e-9);

        const double w = 1600, h = 900;
        var target = new Vector3d(200.000123456, 0.000456789, -0.000321);
        var offset = new Vector3d(1e-3, 0.5e-3, -0.3e-3);
        var cam = new OrbitCamera { Target = target, DistanceMpc = 0.01, YawDeg = 30, PitchDeg = 20 };
        var gpu = ShaderProject(cam, target + offset, w, h);

        // The float relative-to-eye path matches the exact double projection.
        Assert.True(new Projector(cam, w, h).TryProject(target + offset, out var exact));
        Assert.True((gpu - exact).Length < 0.01, $"float vs double: {(gpu - exact).Length} px");

        // Nudging the target (and the point with it) by 1e-6 Mpc does not make the point jitter.
        var nudge = new Vector3d(1e-6, 1e-6, 1e-6);
        cam.Target = target + nudge;
        var gpuNudged = ShaderProject(cam, target + nudge + offset, w, h);
        Assert.True((gpuNudged - gpu).Length < 0.01, $"jitter: {(gpuNudged - gpu).Length} px");
    }

    /// <summary>Emulates points.vert in single precision: rel = (posHigh - eyeHigh) + (posLow - eyeLow).</summary>
    private static Point ShaderProject(OrbitCamera cam, Vector3d world, double w, double h)
    {
        var (pHigh, pLow) = OrbitCamera.Split(world);
        var (eHigh, eLow) = OrbitCamera.Split(cam.Eye);
        Vector3 rel = (pHigh - eHigh) + (pLow - eLow);
        var viewProj = GlMath.ToMatrix4(cam.View * cam.Projection(w / h));
        Vector4 clip = new Vector4(rel, 1f) * viewProj;
        float nx = clip.X / clip.W, ny = clip.Y / clip.W;
        return new Point((nx + 1) * 0.5 * w, (1 - ny) * 0.5 * h);
    }
}
