using Universe3D.Rendering;

namespace Universe3D.Tests;

public class ShaderSourceTests
{
    // OpenTK passes the source length in chars, so any multi-byte UTF-8 character truncates the shader
    // ("pre-mature EOF" on Intel). Keep every shader pure ASCII.
    [Theory]
    [InlineData("points.vert")]
    [InlineData("points.frag")]
    [InlineData("arrows.vert")]
    [InlineData("arrows.geom")]
    [InlineData("arrows.frag")]
    public void Shader_IsEmbeddedAsciiGlsl410Core(string name)
    {
        string source = ShaderProgram.ReadSource(name);
        Assert.StartsWith("#version 410 core", source);
        var nonAscii = source.Where(c => c > 127).ToArray();
        Assert.True(nonAscii.Length == 0, $"{name} has non-ASCII characters: {new string(nonAscii)}");
    }
}
