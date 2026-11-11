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
