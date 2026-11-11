namespace Universe3D.Core.Display;

/// <summary>Defaults for the quasar limit and the galaxy-shape cap, by GPU class (spec §1).</summary>
public sealed record GpuDefaults(int QuasarLimit, int ShapeCap, bool Reduced)
{
    public static GpuDefaults For(GpuClass gpu) => gpu == GpuClass.Minimum
        ? new GpuDefaults(100_000, 5_000, Reduced: true)
        : new GpuDefaults(250_000, 20_000, Reduced: false);
}
