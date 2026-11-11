using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Universe3D.Rendering;

/// <summary>One object as the shaders see it (attribute locations 0–8 in that order).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PointVertex
{
    public Vector3 PosHigh;
    public Vector3 PosLow;
    public float DistanceMpc;
    public uint TypeCode;
    /// <summary>Observed velocity, km/s (c·z when z &gt; 0.1, else cz); NaN for none.</summary>
    public float Cz;
    public float Pec;
    public float Magnitude;
    public float DiameterMpc;
    public uint Index;

    public static readonly int SizeInBytes = Marshal.SizeOf<PointVertex>();
}

/// <summary>What <c>shapes.frag</c> draws for an instance.</summary>
public enum ShapeKind : uint { Ellipsoid, Spiral, BarredSpiral, Lenticular, Irregular, Sphere }

/// <summary>
/// One ellipsoid impostor (galaxy shape, cluster sphere or the Milky Way), attribute locations 1–9 in that order.
/// The ellipsoid has semi-axes <see cref="SemiAxesMpc"/> along its local x, y, z, rotated into the display frame.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ShapeInstance
{
    public Vector3 PosHigh;
    public Vector3 PosLow;
    /// <summary>Quaternion (x, y, z, w) taking local axes to the display frame.</summary>
    public Vector4 Rotation;
    public Vector3 SemiAxesMpc;
    public ShapeKind Kind;
    public uint TypeCode;
    /// <summary>Observed velocity, km/s; NaN for none.</summary>
    public float Cz;
    /// <summary>0–1, varies the procedural pattern.</summary>
    public float Seed;
    public float DistanceMpc;

    public static readonly int SizeInBytes = Marshal.SizeOf<ShapeInstance>();
}

/// <summary>A corner quad (location 0) drawn once per <see cref="ShapeInstance"/> (locations 1–9).</summary>
public sealed class ShapeInstanceBuffer : IDisposable
{
    private static readonly float[] Corners = { -1, -1, 1, -1, -1, 1, 1, 1 };

    private int _vao, _cornerVbo, _instanceVbo, _capacity;

    /// <summary>Requires a current GL context.</summary>
    public ShapeInstanceBuffer()
    {
        _vao = GL.GenVertexArray();
        _cornerVbo = GL.GenBuffer();
        _instanceVbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _cornerVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Corners.Length * sizeof(float), Corners, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
        int stride = ShapeInstance.SizeInBytes;
        Float(1, 3, nameof(ShapeInstance.PosHigh));
        Float(2, 3, nameof(ShapeInstance.PosLow));
        Float(3, 4, nameof(ShapeInstance.Rotation));
        Float(4, 3, nameof(ShapeInstance.SemiAxesMpc));
        UInt(5, nameof(ShapeInstance.Kind));
        UInt(6, nameof(ShapeInstance.TypeCode));
        Float(7, 1, nameof(ShapeInstance.Cz));
        Float(8, 1, nameof(ShapeInstance.Seed));
        Float(9, 1, nameof(ShapeInstance.DistanceMpc));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        void Float(int location, int size, string field)
        {
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, stride, Offset(field));
            GL.VertexAttribDivisor(location, 1);
        }

        void UInt(int location, string field)
        {
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribIPointer(location, 1, VertexAttribIntegerType.UnsignedInt, stride, (IntPtr)Offset(field));
            GL.VertexAttribDivisor(location, 1);
        }

        static int Offset(string field) => (int)Marshal.OffsetOf<ShapeInstance>(field);
    }

    public int Count { get; private set; }

    /// <summary>Replaces the instances with the first <paramref name="count"/> items.</summary>
    public void Upload(ShapeInstance[] items, int count)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
        if (count > _capacity)
        {
            _capacity = Math.Max(count, 2 * _capacity);
            GL.BufferData(BufferTarget.ArrayBuffer, _capacity * ShapeInstance.SizeInBytes, IntPtr.Zero,
                          BufferUsageHint.DynamicDraw);
        }
        if (count > 0)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, count * ShapeInstance.SizeInBytes, items);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        Count = count;
    }

    public void Draw()
    {
        if (Count == 0) return;
        GL.BindVertexArray(_vao);
        GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, Count);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_cornerVbo != 0) GL.DeleteBuffer(_cornerVbo);
        if (_instanceVbo != 0) GL.DeleteBuffer(_instanceVbo);
        _vao = _cornerVbo = _instanceVbo = 0;
    }
}

/// <summary>The point vertex buffer, shared by the point cloud and the velocity arrows.</summary>
public sealed class GpuBuffers : IDisposable
{
    private readonly List<int> _vaos = new();

    public GpuBuffers() => PointVbo = GL.GenBuffer();

    public int PointVbo { get; private set; }

    public int PointCount { get; private set; }

    public void UploadPoints(PointVertex[] vertices)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, PointVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * PointVertex.SizeInBytes, vertices,
                      BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        PointCount = vertices.Length;
    }

    /// <summary>A vertex array reading <see cref="PointVertex"/> records from the shared buffer.</summary>
    public int CreatePointVao()
    {
        int vao = GL.GenVertexArray();
        _vaos.Add(vao);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, PointVbo);
        int stride = PointVertex.SizeInBytes;
        Float(0, 3, nameof(PointVertex.PosHigh));
        Float(1, 3, nameof(PointVertex.PosLow));
        Float(2, 1, nameof(PointVertex.DistanceMpc));
        UInt(3, nameof(PointVertex.TypeCode));
        Float(4, 1, nameof(PointVertex.Cz));
        Float(5, 1, nameof(PointVertex.Pec));
        Float(6, 1, nameof(PointVertex.Magnitude));
        Float(7, 1, nameof(PointVertex.DiameterMpc));
        UInt(8, nameof(PointVertex.Index));
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        return vao;

        void Float(int location, int size, string field)
        {
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, stride, Offset(field));
        }

        void UInt(int location, string field)
        {
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribIPointer(location, 1, VertexAttribIntegerType.UnsignedInt, stride, (IntPtr)Offset(field));
        }
    }

    private static int Offset(string field) => (int)Marshal.OffsetOf<PointVertex>(field);

    public void Dispose()
    {
        foreach (int vao in _vaos) GL.DeleteVertexArray(vao);
        _vaos.Clear();
        if (PointVbo != 0) GL.DeleteBuffer(PointVbo);
        PointVbo = 0;
    }
}
