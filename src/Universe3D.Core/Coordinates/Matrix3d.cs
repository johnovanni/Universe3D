namespace Universe3D.Core.Coordinates;

/// <summary>Immutable 3x3 matrix, row-major.</summary>
public readonly struct Matrix3d
{
    private readonly double _m00, _m01, _m02, _m10, _m11, _m12, _m20, _m21, _m22;

    public Matrix3d(double m00, double m01, double m02,
                    double m10, double m11, double m12,
                    double m20, double m21, double m22)
    {
        _m00 = m00; _m01 = m01; _m02 = m02;
        _m10 = m10; _m11 = m11; _m12 = m12;
        _m20 = m20; _m21 = m21; _m22 = m22;
    }

    public static Matrix3d Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public static Matrix3d FromRows(Vector3d r0, Vector3d r1, Vector3d r2) =>
        new(r0.X, r0.Y, r0.Z, r1.X, r1.Y, r1.Z, r2.X, r2.Y, r2.Z);

    public double this[int row, int col] => (row, col) switch
    {
        (0, 0) => _m00, (0, 1) => _m01, (0, 2) => _m02,
        (1, 0) => _m10, (1, 1) => _m11, (1, 2) => _m12,
        (2, 0) => _m20, (2, 1) => _m21, (2, 2) => _m22,
        _ => throw new IndexOutOfRangeException(),
    };

    public Matrix3d Transpose() => new(_m00, _m10, _m20, _m01, _m11, _m21, _m02, _m12, _m22);

    public static Vector3d operator *(Matrix3d m, Vector3d v) => new(
        m._m00 * v.X + m._m01 * v.Y + m._m02 * v.Z,
        m._m10 * v.X + m._m11 * v.Y + m._m12 * v.Z,
        m._m20 * v.X + m._m21 * v.Y + m._m22 * v.Z);

    public static Matrix3d operator *(Matrix3d a, Matrix3d b)
    {
        double E(int r, int c) =>
            a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
        return new(E(0, 0), E(0, 1), E(0, 2), E(1, 0), E(1, 1), E(1, 2), E(2, 0), E(2, 1), E(2, 2));
    }
}
