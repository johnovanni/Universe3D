using System.Globalization;

namespace Universe3D.Core.Text;

public static class Units
{
    private const double MlyPerMpc = 3.2616;
    private static readonly string[] LyUnits = { "ly", "kly", "Mly", "Gly" };

    /// <summary>Distance in light-years with 3 significant digits, e.g. "2.51 Mly".</summary>
    public static string FormatLightYears(double mpc)
    {
        double ly = mpc * MlyPerMpc * 1e6;
        if (!(ly > 0)) return "0 ly";
        int u = ly < 1e3 ? 0 : ly < 1e6 ? 1 : ly < 1e9 ? 2 : 3;
        double v = ly / Math.Pow(1000, u);
        // Rounding to 3 significant digits can push 999.6 up to 1000: step up a unit.
        if (u < 3 && SigRound(v) >= 1000) { v /= 1000; u++; }
        return Sig3(v) + " " + LyUnits[u];
    }

    /// <summary>"2.51 Mly (0.77 Mpc)".</summary>
    public static string FormatDistance(double mpc) => $"{FormatLightYears(mpc)} ({FormatMpc(mpc)} Mpc)";

    private static string FormatMpc(double mpc)
    {
        if (!(mpc > 0)) return "0";
        if (mpc >= 1000 || SigRound(mpc) >= 1000)
            return Math.Round(mpc).ToString("N0", CultureInfo.InvariantCulture);
        string s = Sig3(mpc);
        return s.Contains('.') ? s.TrimEnd('0').TrimEnd('.') : s;
    }

    private static int Decimals(double v) => Math.Max(0, 2 - (int)Math.Floor(Math.Log10(v)));

    private static double SigRound(double v) => Math.Round(v, Decimals(v));

    private static string Sig3(double v)
    {
        double r = SigRound(v);
        return r.ToString("F" + Decimals(r), CultureInfo.InvariantCulture);
    }
}
