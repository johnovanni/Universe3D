using System.IO;
using System.Reflection;
using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Universe3D.Rendering;

/// <summary>A linked GL program built from shader sources embedded under <c>Shaders/</c>.</summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly Dictionary<string, int> _uniforms = new();

    private ShaderProgram(int handle) => Handle = handle;

    public int Handle { get; private set; }

    /// <summary>Compiles and links the named resources, e.g. <c>"points.vert", "points.frag"</c>.</summary>
    public static ShaderProgram FromResources(params string[] fileNames)
    {
        var shaders = new List<int>();
        int program = GL.CreateProgram();
        try
        {
            foreach (var name in fileNames)
            {
                int shader = GL.CreateShader(TypeOf(name));
                shaders.Add(shader);
                GL.ShaderSource(shader, ReadSource(name));
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
                if (ok == 0)
                    throw new InvalidOperationException($"Shader {name} failed to compile:\n{GL.GetShaderInfoLog(shader)}");
                GL.AttachShader(program, shader);
            }
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
                throw new InvalidOperationException(
                    $"Program {string.Join(" + ", fileNames)} failed to link:\n{GL.GetProgramInfoLog(program)}");
        }
        catch
        {
            GL.DeleteProgram(program);
            throw;
        }
        finally
        {
            foreach (int s in shaders)
            {
                GL.DetachShader(program, s);
                GL.DeleteShader(s);
            }
        }
        return new ShaderProgram(program);
    }

    public static string ReadSource(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        string resource = $"{typeof(ShaderProgram).Namespace}.Shaders.{fileName}";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Shader resource '{resource}' is not embedded.", fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Use() => GL.UseProgram(Handle);

    /// <summary>Uniform location, or −1 when the linker removed it (setting −1 is a no-op in GL).</summary>
    public int Location(string name)
    {
        if (!_uniforms.TryGetValue(name, out int loc))
            _uniforms[name] = loc = GL.GetUniformLocation(Handle, name);
        return loc;
    }

    public void Set(string name, float v) => GL.Uniform1(Location(name), v);
    public void Set(string name, int v) => GL.Uniform1(Location(name), v);
    public void Set(string name, uint v) => GL.Uniform1(Location(name), v);
    public void Set(string name, Vector2 v) => GL.Uniform2(Location(name), v);
    public void Set(string name, Vector3 v) => GL.Uniform3(Location(name), v);
    public void Set(string name, Vector4 v) => GL.Uniform4(Location(name), v);

    public void Set(string name, Matrix4 m) => GL.UniformMatrix4(Location(name), false, ref m);

    public void Set(string name, Vector3[] values)
    {
        var flat = new float[values.Length * 3];
        for (int i = 0; i < values.Length; i++)
            (flat[3 * i], flat[3 * i + 1], flat[3 * i + 2]) = (values[i].X, values[i].Y, values[i].Z);
        GL.Uniform3(Location(name), values.Length, flat);
    }

    /// <summary>
    /// Sets the shared camera uniforms: <c>u_eyeHigh</c>/<c>u_eyeLow</c> (relative-to-eye split of the eye),
    /// <c>u_viewProj</c> (rotation-only view times projection), <c>u_focalPx</c> and <c>u_viewport</c>.
    /// </summary>
    public void SetCamera(OrbitCamera cam, Size viewportPx)
    {
        var (eyeHigh, eyeLow) = OrbitCamera.Split(cam.Eye);
        Set("u_eyeHigh", eyeHigh);
        Set("u_eyeLow", eyeLow);
        double aspect = viewportPx.Height > 0 ? viewportPx.Width / viewportPx.Height : 1;
        Set("u_viewProj", GlMath.ToMatrix4(cam.View * cam.Projection(aspect)));
        Set("u_focalPx", (float)(viewportPx.Height / (2 * Math.Tan(cam.FieldOfViewDeg * Math.PI / 360))));
        Set("u_viewport", new Vector2((float)viewportPx.Width, (float)viewportPx.Height));
    }

    private static ShaderType TypeOf(string fileName) => Path.GetExtension(fileName) switch
    {
        ".vert" => ShaderType.VertexShader,
        ".geom" => ShaderType.GeometryShader,
        ".frag" => ShaderType.FragmentShader,
        _ => throw new ArgumentException($"Unknown shader stage for '{fileName}'.", nameof(fileName)),
    };

    public void Dispose()
    {
        if (Handle != 0) GL.DeleteProgram(Handle);
        Handle = 0;
    }
}
