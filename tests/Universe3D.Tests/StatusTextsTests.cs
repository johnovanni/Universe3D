using Universe3D.Core.Text;

namespace Universe3D.Tests;

public class StatusTextsTests
{
    /// <summary>Spec §7: skipped catalog rows are counted in the status bar.</summary>
    [Fact]
    public void Skipped_CountsRowsOrIsEmpty()
    {
        Assert.Equal("1,063 catalog rows skipped (incomplete)", StatusTexts.Skipped(1063));
        Assert.Equal("1 catalog row skipped (incomplete)", StatusTexts.Skipped(1));
        Assert.Equal("", StatusTexts.Skipped(0));
    }

    [Fact]
    public void Excluded_CountsObjectsOrIsEmpty()
    {
        Assert.Equal("2,048 left out (no valid distance)", StatusTexts.Excluded(2048));
        Assert.Equal("", StatusTexts.Excluded(0));
    }

    /// <summary>Once the download is saved, a later failure must not claim the existing data was kept.</summary>
    [Fact]
    public void DownloadFailed_SaysWhetherTheNewCopyWasSaved()
    {
        string before = StatusTexts.DownloadFailed("Nearby universe", "Disk full.", saved: false);
        Assert.Equal("Download of Nearby universe failed; the existing data was kept. Disk full.", before);

        string after = StatusTexts.DownloadFailed("Nearby universe", "Out of memory.", saved: true);
        Assert.DoesNotContain("existing data was kept", after);
        Assert.Contains("saved", after);
        Assert.EndsWith("Out of memory.", after);
    }
}
