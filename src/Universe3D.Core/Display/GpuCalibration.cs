namespace Universe3D.Core.Display;

/// <summary>
/// Frame-time measurement for a GPU its renderer name does not identify (spec §1). Once a data set is drawn, the
/// first <see cref="SkipFrames"/> frames are skipped and the median of the next <see cref="Frames"/> classifies the
/// GPU. If no data set is drawn within <see cref="MaxWait"/>, it gives up, so the view does not keep redrawing
/// forever; the current defaults then stay.
/// </summary>
public sealed class GpuCalibration(string renderer)
{
    public const int SkipFrames = 3, Frames = 60;
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly List<double> _ms = new();

    /// <summary>True until a class is found or the wait is over.</summary>
    public bool IsRunning { get; private set; } = true;

    /// <summary>True when it stopped because no data set was drawn in time.</summary>
    public bool GaveUp { get; private set; }

    /// <summary>
    /// Feeds one frame: time since calibration started, whether a data set is drawn, and the frame's draw time.
    /// Returns the GPU class on the frame that completes the measurement, otherwise null.
    /// </summary>
    public GpuClass? Step(TimeSpan sinceStart, bool dataShown, double drawMs)
    {
        if (!IsRunning) return null;
        if (!dataShown)
        {
            if (_ms.Count == 0 && sinceStart >= MaxWait)
            {
                IsRunning = false;
                GaveUp = true;
            }
            return null;
        }
        _ms.Add(drawMs);
        if (_ms.Count < SkipFrames + Frames) return null;
        IsRunning = false;
        var sample = _ms.Skip(SkipFrames).OrderBy(x => x).ToArray();
        return GpuClassifier.Classify(renderer, sample[sample.Length / 2]);
    }
}
