namespace Universe3D.Core.Coordinates;

/// <summary>Immutable double-precision 3D vector.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(double s, Vector3d a) => a * s;
    public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public double Dot(Vector3d o) => X * o.X + Y * o.Y + Z * o.Z;

    public Vector3d Cross(Vector3d o) =>
        new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);

    public double Length => Math.Sqrt(Dot(this));

    public Vector3d Normalized()
    {
        var len = Length;
        return len == 0 ? this : this / len;
    }
}
