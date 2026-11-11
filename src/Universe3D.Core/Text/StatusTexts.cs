using System.Globalization;

namespace Universe3D.Core.Text;

/// <summary>Status-bar and catalog-area texts about a data set and its download.</summary>
public static class StatusTexts
{
    /// <summary>"2,048 left out (no valid distance)", or empty.</summary>
    public static string Excluded(int count) =>
        count > 0 ? string.Create(CultureInfo.InvariantCulture, $"{count:N0} left out (no valid distance)") : "";

    /// <summary>Spec §7: "1,063 catalog rows skipped (incomplete)", or empty.</summary>
    public static string Skipped(int count) =>
        count > 0
            ? string.Create(CultureInfo.InvariantCulture,
                $"{count:N0} catalog {(count == 1 ? "row" : "rows")} skipped (incomplete)")
            : "";

    /// <summary>
    /// A failed "Download latest". Before the new copy is saved, the existing data is untouched; after, the new copy
    /// is on disk (and loads next time) even though showing it failed.
    /// </summary>
    public static string DownloadFailed(string title, string reason, bool saved) => saved
        ? $"{title} was downloaded and saved, but could not be shown. {reason}"
        : $"Download of {title} failed; the existing data was kept. {reason}";
}
