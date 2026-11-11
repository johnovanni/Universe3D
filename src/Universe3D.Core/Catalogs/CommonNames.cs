using System.Globalization;
using System.Text.RegularExpressions;

namespace Universe3D.Core.Catalogs;

/// <summary>Messier and popular-name tables plus catalog designation normalisation.</summary>
public static class CommonNames
{
    public static IReadOnlyDictionary<string, string> Messier { get; } = new Dictionary<string, string>
    {
        ["M31"] = "NGC 224", ["M32"] = "NGC 221", ["M33"] = "NGC 598", ["M49"] = "NGC 4472",
        ["M51"] = "NGC 5194", ["M58"] = "NGC 4579", ["M59"] = "NGC 4621", ["M60"] = "NGC 4649",
        ["M61"] = "NGC 4303", ["M63"] = "NGC 5055", ["M64"] = "NGC 4826", ["M65"] = "NGC 3623",
        ["M66"] = "NGC 3627", ["M74"] = "NGC 628", ["M77"] = "NGC 1068", ["M81"] = "NGC 3031",
        ["M82"] = "NGC 3034", ["M83"] = "NGC 5236", ["M84"] = "NGC 4374", ["M85"] = "NGC 4382",
        ["M86"] = "NGC 4406", ["M87"] = "NGC 4486", ["M88"] = "NGC 4501", ["M89"] = "NGC 4552",
        ["M90"] = "NGC 4569", ["M91"] = "NGC 4548", ["M94"] = "NGC 4736", ["M95"] = "NGC 3351",
        ["M96"] = "NGC 3368", ["M98"] = "NGC 4192", ["M99"] = "NGC 4254", ["M100"] = "NGC 4321",
        ["M101"] = "NGC 5457", ["M102"] = "NGC 5866", ["M104"] = "NGC 4594", ["M105"] = "NGC 3379",
        ["M106"] = "NGC 4258", ["M108"] = "NGC 3556", ["M109"] = "NGC 3992", ["M110"] = "NGC 205",
    };

    /// <summary>Designation (normalised) or MCXC name to popular name.</summary>
    public static IReadOnlyDictionary<string, string> Popular { get; } = new Dictionary<string, string>
    {
        ["NGC 224"] = "Andromeda Galaxy", ["NGC 598"] = "Triangulum Galaxy",
        ["NGC 5194"] = "Whirlpool Galaxy", ["NGC 4594"] = "Sombrero Galaxy",
        ["NGC 5457"] = "Pinwheel Galaxy", ["NGC 3034"] = "Cigar Galaxy",
        ["NGC 3031"] = "Bode's Galaxy", ["NGC 4486"] = "Virgo A", ["NGC 5128"] = "Centaurus A",
        ["LMC"] = "Large Magellanic Cloud", ["SMC"] = "Small Magellanic Cloud",
        ["Abell 1656"] = "Coma Cluster", ["Abell 426"] = "Perseus Cluster",
        ["Abell 2151"] = "Hercules Cluster", ["Abell 3627"] = "Norma Cluster",
        ["Abell 3526"] = "Centaurus Cluster",
        ["Virgo"] = "Virgo Cluster", ["Fornax"] = "Fornax Cluster",
    };

    /// <summary>Fallback positions (Ruling R3) for clusters whose MCXC name does not match.</summary>
    public static IReadOnlyList<(string Name, double RaDeg, double DecDeg)> ClusterPositions { get; } =
    [
        ("Virgo Cluster", 187.70, 12.39),
        ("Fornax Cluster", 54.62, -35.45),
    ];

    private static readonly Regex Prefixed = new(@"^(NGC|IC|UGC)[\s_]*0*(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Messr = new(@"^(?:MESSIER|M)[\s_]*0*(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AbellRx = new(@"^(?:ABELL|ACO|A)[\s_]*0*(\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string NormalizeDesignation(string raw)
    {
        string s = raw.Trim();
        var m = Prefixed.Match(s);
        if (m.Success)
            return string.Create(CultureInfo.InvariantCulture, $"{m.Groups[1].Value.ToUpperInvariant()} {int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)}");
        m = Messr.Match(s);
        if (m.Success)
            return string.Create(CultureInfo.InvariantCulture, $"M {int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)}");
        m = AbellRx.Match(s);
        if (m.Success)
            return string.Create(CultureInfo.InvariantCulture, $"Abell {int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)}");
        return s;
    }

    /// <summary>
    /// Comparison keys of a catalog name: the normalised designation without spaces, brackets or underscores
    /// (so "Leo I", "LeoI" and "Bootes (I)" / "BootesI" agree), plus the NGC equivalent of a Messier object and
    /// vice versa. Two names refer to the same object when their key sets intersect.
    /// </summary>
    public static IReadOnlyList<string> MatchKeys(string? raw)
    {
        var keys = new List<string>(2);
        if (string.IsNullOrWhiteSpace(raw)) return keys;
        string n = NormalizeDesignation(raw);
        keys.Add(Squash(n));
        if (n.StartsWith("M ", StringComparison.Ordinal) && Messier.TryGetValue("M" + n[2..], out var ngc))
            keys.Add(Squash(ngc));
        else
            foreach (var kv in Messier)
                if (kv.Value == n) { keys.Add(Squash(kv.Key)); break; }
        return keys;
    }

    private static string Squash(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
