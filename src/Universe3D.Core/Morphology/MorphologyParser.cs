using System.Text.RegularExpressions;

namespace Universe3D.Core.Morphology;

/// <summary>Turns each catalog's galaxy-type code into a <see cref="MorphologyInfo"/>.</summary>
public static class MorphologyParser
{
    // de Vaucouleurs T number -> Hubble-type name.
    private static readonly Dictionary<int, string> TNames = new()
    {
        [-6] = "cE", [-5] = "E", [-4] = "E+", [-3] = "S0-", [-2] = "S0", [-1] = "S0+", [0] = "S0/a",
        [1] = "Sa", [2] = "Sab", [3] = "Sb", [4] = "Sbc", [5] = "Sc", [6] = "Scd", [7] = "Sd", [8] = "Sdm",
        [9] = "Sm", [10] = "Im",
    };

    private static readonly Regex ZcatPattern = new(@"^\s*(-?\d{1,2})([ABX_]?)(.*)$", RegexOptions.Compiled);
    private static readonly Regex Parenthesised = new(@"\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex SpiralPattern = new(@"^S(AB|B|A)?(ab|bc|cd|dm|a|b|c|d|m)?$", RegexOptions.Compiled);

    /// <summary>2MRS/ZCAT code, e.g. " 3A2s": T number, bar (A/B/X/_), and P anywhere after the T for peculiar.</summary>
    public static MorphologyInfo FromZcat(string? code)
    {
        string original = (code ?? "").Trim();
        var m = ZcatPattern.Match(code ?? "");
        if (!m.Success)
            return Unknown(original);

        int t = int.Parse(m.Groups[1].Value);
        char bar = m.Groups[2].Length > 0 ? m.Groups[2].Value[0] : '_';
        bool peculiar = m.Groups[3].Value.Contains('P');
        return FromT(t, bar, original, peculiar);
    }

    /// <summary>UNGC: <c>TT</c> plus <c>Tdw</c> (surface-brightness class letter for dwarfs, blank otherwise).</summary>
    public static MorphologyInfo FromUngc(int? t, string? surfaceBrightnessClass)
    {
        string sb = (surfaceBrightnessClass ?? "").Trim();
        string original = t is null ? sb : sb.Length > 0 ? $"{t} {sb}" : t.Value.ToString();
        if (t is null)
            return Unknown(original);

        if (sb.Length > 0)
        {
            if (t <= -1)
                return new(MorphologyClass.DwarfSpheroidal, "Dwarf spheroidal", original, false);
            if (t >= 9)
                return new(MorphologyClass.DwarfIrregular, "Dwarf irregular", original, false);
        }
        return FromT(t.Value, '_', original, false);
    }

    /// <summary>McConnachie Local Group catalog type, e.g. "dSph", "dIrr/dSph", "dE4", "cE2", "Sb".</summary>
    public static MorphologyInfo FromMcConnachie(string? mType)
    {
        string original = (mType ?? "").Trim();
        string s = original.TrimEnd('?');
        if (s.Length == 0)
            return Unknown(original);

        bool hasIrr = s.Contains("dIrr", StringComparison.OrdinalIgnoreCase);
        bool hasSph = s.Contains("dSph", StringComparison.OrdinalIgnoreCase);
        if (hasIrr && hasSph)
        {
            // Transition type: the first-named kind decides the class.
            bool irrFirst = s.IndexOf("dIrr", StringComparison.OrdinalIgnoreCase)
                            < s.IndexOf("dSph", StringComparison.OrdinalIgnoreCase);
            return new(irrFirst ? MorphologyClass.DwarfIrregular : MorphologyClass.DwarfSpheroidal,
                "Dwarf irregular/spheroidal transition type", original, false);
        }
        if (hasIrr)
            return new(MorphologyClass.DwarfIrregular, "Dwarf irregular", original, false);
        if (hasSph || Regex.IsMatch(s, @"^d(E|S0)"))
            return new(MorphologyClass.DwarfSpheroidal, "Dwarf spheroidal", original, false);

        return ParseLetters(s, original);
    }

    /// <summary>HyperLEDA MType, e.g. "Sbc", "SBb", "SO-a" (letter O), "E-SO", "E?", "-----", blank.</summary>
    public static MorphologyInfo FromHyperLeda(string? mType)
    {
        string original = (mType ?? "").Trim();
        string s = original.TrimEnd('?');
        if (s.Length == 0 || s.All(c => c == '-'))
            return Unknown(original);
        return ParseLetters(s, original);
    }

    // Shared Hubble-letter parser (HyperLEDA and the non-dwarf McConnachie types).
    private static MorphologyInfo ParseLetters(string s, string original)
    {
        s = Parenthesised.Replace(s, "");
        // HyperLEDA writes S0 with the letter O; E-S0 is the E/S0 intermediate type.
        if (Regex.IsMatch(s, @"^(E-?)?S[0O]"))
            return new(MorphologyClass.Lenticular, "Lenticular (S0)", original, false);

        if (Regex.IsMatch(s, @"^c?E"))
            return new(MorphologyClass.Elliptical, "Elliptical", original, false);

        if (Regex.IsMatch(s, @"^I(rr|m|)$", RegexOptions.IgnoreCase))
            return new(MorphologyClass.Irregular, "Irregular", original, false);

        var m = SpiralPattern.Match(s);
        if (m.Success)
        {
            string bar = m.Groups[1].Value;
            string sub = m.Groups[2].Value;
            string kind = bar switch
            {
                "B" => "barred spiral",
                "AB" => "spiral (mixed bar)",
                _ => "spiral",
            };
            string name = (bar == "B" ? "SB" : "S") + (sub.Length > 0 ? sub : "");
            string desc = sub.Length > 0 ? $"{name} {kind}" : $"Spiral (subtype unknown)";
            if (sub.Length == 0 && bar == "B")
                desc = "Barred spiral (subtype unknown)";
            return new(bar == "B" ? MorphologyClass.BarredSpiral : MorphologyClass.Spiral, desc, original, false);
        }
        return Unknown(original);
    }

    private static MorphologyInfo FromT(int t, char bar, string original, bool peculiar)
    {
        string name = TNames.TryGetValue(t, out var n) ? n : t < -6 ? "E" : "";
        string suffix = peculiar ? ", peculiar" : "";

        if (t <= -4)
            return new(MorphologyClass.Elliptical, $"{name} elliptical{suffix}", original, peculiar);
        if (t <= 0)
            return new(MorphologyClass.Lenticular, $"{name} lenticular{suffix}", original, peculiar);
        if (t <= 8)
        {
            bool barred = bar == 'B';
            string barText = bar switch
            {
                'A' => " (unbarred)",
                'B' => " (barred)",
                'X' => " (mixed bar)",
                _ => "",
            };
            return new(barred ? MorphologyClass.BarredSpiral : MorphologyClass.Spiral,
                $"{name} spiral{barText}{suffix}", original, peculiar);
        }
        if (t <= 10)
            return new(MorphologyClass.Irregular, $"{name} irregular{suffix}", original, peculiar);

        return peculiar
            ? new(MorphologyClass.Peculiar, "Peculiar", original, true)
            : Unknown(original);
    }

    private static MorphologyInfo Unknown(string original) =>
        new(MorphologyClass.Unknown, "Unknown morphology", original, false);
}
