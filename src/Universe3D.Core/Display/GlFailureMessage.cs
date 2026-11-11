using System.Globalization;
using System.Text.RegularExpressions;

namespace Universe3D.Core.Display;

/// <summary>The OpenGL 4.1 requirement check and the message shown when it is not met (spec §7).</summary>
public static class GlFailureMessage
{
    private static readonly Regex VersionRx = new(@"^\s*(\d+)\.(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True when a GL_VERSION string (for example "4.6.0 NVIDIA 551.23") is 4.1 or later.</summary>
    public static bool IsSupported(string? glVersion)
    {
        if (glVersion == null || VersionRx.Match(glVersion) is not { Success: true } m) return false;
        int major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int minor = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return major > 4 || (major == 4 && minor >= 1);
    }

    /// <summary>
    /// Names the requirement (OpenGL 4.1), the detected renderer (and version) if any, the reason if known, and the
    /// hint for USB display adapters.
    /// </summary>
    public static string Build(string? renderer, string? glVersion, string? reason)
    {
        string found = string.IsNullOrWhiteSpace(renderer)
            ? "Detected graphics: not detected (no OpenGL context could be created)."
            : $"Detected graphics: {renderer}" + (string.IsNullOrWhiteSpace(glVersion) ? "." : $", OpenGL {glVersion}.");
        string detail = string.IsNullOrWhiteSpace(reason) ? "" : $"\n\nDetails: {reason}";
        return "Universe3D needs OpenGL 4.1 (core profile), and this window could not get it.\n\n" + found +
               "\n\nUpdate the graphics driver, or, if this window is on a USB display adapter (DisplayLink or similar), " +
               "move it to a screen that is driven directly by the GPU, such as the laptop's own display." + detail;
    }
}
