using System.Text.RegularExpressions;

namespace Universe3D.Core.Display;

public enum GpuClass { Minimum, Recommended }

/// <summary>Sorts a GPU into the spec's minimum or recommended class from its GL renderer string, else from a timing.</summary>
public static class GpuClassifier
{
    /// <summary>Median frame time (ms) under which an unrecognised GPU counts as recommended, over 60 frames of 250,000 points.</summary>
    public const double RecommendedFrameMs = 8;

    private static readonly Regex RecommendedRx = new(
        @"Iris\(R\) Xe|Arc|RTX|GTX 1[0-9]|GTX 16|Radeon RX|Radeon\(TM\) 6[0-9]0M|Radeon 7",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MinimumRx = new(
        @"UHD|HD Graphics|Vega [0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static GpuClass Classify(string glRenderer, double medianFrameMs)
    {
        glRenderer ??= "";
        if (RecommendedRx.IsMatch(glRenderer)) return GpuClass.Recommended;
        if (MinimumRx.IsMatch(glRenderer)) return GpuClass.Minimum;
        return medianFrameMs < RecommendedFrameMs ? GpuClass.Recommended : GpuClass.Minimum;
    }
}
