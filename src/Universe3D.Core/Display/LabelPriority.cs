using Universe3D.Core.Catalogs;
using Universe3D.Core.Objects;

namespace Universe3D.Core.Display;

/// <summary>
/// The order in which objects get name labels (spec §4): well-known names first (Messier/NGC galaxies, clusters with
/// a popular name, 3C/PKS quasars), each group by brightness, then every other object by brightness. Brightness is the
/// magnitude where there is one, then the size. The selected object and the Milky Way come before all of these and
/// are labeled by the caller, so the Milky Way object itself is left out.
/// </summary>
public static class LabelPriority
{
    private static readonly HashSet<string> PopularNames = new(CommonNames.Popular.Values, StringComparer.OrdinalIgnoreCase);

    public static int[] Order(IReadOnlyList<CosmicObject> objects)
    {
        var keys = new List<(int Tier, int NoMag, double Mag, double NegSize, int Index)>(objects.Count);
        for (int i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (IsMilkyWay(o)) continue;
            bool hasMag = o.Magnitude is double m && double.IsFinite(m);
            keys.Add((IsWellKnown(o) ? 0 : 1, hasMag ? 0 : 1, hasMag ? o.Magnitude!.Value : 0, -Size(o), i));
        }
        keys.Sort();
        return keys.Select(k => k.Index).ToArray();
    }

    public static bool IsMilkyWay(CosmicObject o) =>
        o.Name == "Milky Way" || o.AltNames.Contains("Milky Way", StringComparer.OrdinalIgnoreCase);

    private static bool IsWellKnown(CosmicObject o) => o switch
    {
        Galaxy => o.AltNames.Prepend(o.Name).Any(n =>
            n.StartsWith("M ", StringComparison.Ordinal) || n.StartsWith("NGC ", StringComparison.Ordinal)),
        GalaxyCluster => o.AltNames.Prepend(o.Name).Any(n =>
            PopularNames.Contains(n) || PopularNames.Any(p => n.Contains(p, StringComparison.OrdinalIgnoreCase))),
        Quasar => o.Name.StartsWith("3C ", StringComparison.Ordinal) || o.Name.StartsWith("PKS ", StringComparison.Ordinal),
        _ => false,
    };

    private static double Size(CosmicObject o) => o switch
    {
        Galaxy { DiameterKpc: double d } when double.IsFinite(d) => d,
        GalaxyCluster { M500e14: double m } when double.IsFinite(m) => m,
        _ => 0,
    };
}
