using OpenTK.Mathematics;

namespace Universe3D.Rendering;

/// <summary>Double-to-single conversions for values sent to the GPU.</summary>
public static class GlMath
{
    public static Matrix4 ToMatrix4(Matrix4d m) => new(
        (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
        (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
        (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
        (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44);
}
