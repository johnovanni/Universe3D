using System.Diagnostics;
using System.Windows;
using Universe3D.Core.Display;

namespace Universe3D.App.Controllers;

/// <summary>Thrown inside the first frame when the context exists but is older than OpenGL 4.1.</summary>
internal sealed class GlUnsupportedException(string? renderer, string? version) : Exception("OpenGL 4.1 is not available.")
{
    public string? Renderer { get; } = renderer;
    public string? Version { get; } = version;
}

/// <summary>GL requirement check, GPU classification and the reduced defaults (spec §1 and §7).</summary>
public sealed partial class SceneController
{
    private GpuCalibration? _calibration;
    private readonly Stopwatch _calibrationClock = new();

    /// <summary>True while an unrecognised GPU is being measured (the view keeps redrawing meanwhile).</summary>
    private bool Calibrating => _calibration is { IsRunning: true };

    private static (string? Renderer, string? Version) SplitGlInfo(string glInfo)
    {
        var parts = glInfo.Split('|', StringSplitOptions.TrimEntries);
        return (parts.ElementAtOrDefault(1), parts.ElementAtOrDefault(2));
    }

    /// <summary>Throws <see cref="GlUnsupportedException"/> when the context is older than OpenGL 4.1.</summary>
    private static void RequireOpenGl41(string glInfo)
    {
        var (renderer, version) = SplitGlInfo(glInfo);
        if (!GlFailureMessage.IsSupported(version)) throw new GlUnsupportedException(renderer, version);
    }

    /// <summary>
    /// Classifies the GPU by its renderer string. If the name is not recognised, measures the GPU for the first ~60
    /// frames of the initial view (<see cref="CalibrationStep"/>) and classifies by the median. If no data set is drawn
    /// within <see cref="GpuCalibration.MaxWait"/>, it stops and keeps the current defaults.
    /// </summary>
    private void BeginGpuClassification(string glInfo)
    {
        string renderer = SplitGlInfo(glInfo).Renderer ?? "";
        var byName = GpuClassifier.Classify(renderer, 0);
        if (byName == GpuClassifier.Classify(renderer, double.MaxValue))
        {
            _vm.ApplyGpuDefaults(GpuDefaults.For(byName));
            return;
        }
        _calibration = new GpuCalibration(renderer);
        _calibrationClock.Restart();
        _host.MeasureDraw = true;
    }

    /// <summary>
    /// Called after each frame. Collects frame costs once the first data set is drawn, then applies the defaults; gives
    /// up if no data set is drawn in time.
    /// </summary>
    private void CalibrationStep()
    {
        if (_calibration is not { IsRunning: true } calibration) return;
        var gpu = calibration.Step(_calibrationClock.Elapsed, _uploaded != null, _host.LastDrawMs);
        if (calibration.IsRunning) return;
        _host.MeasureDraw = false;
        _calibrationClock.Stop();
        if (gpu is { } measured) _vm.ApplyGpuDefaults(GpuDefaults.For(measured));
    }

    /// <summary>The OpenGL 4.1 message (spec §7): requirement, detected renderer, USB-adapter hint. Does not close the window.</summary>
    private void ShowGlFailure(string? renderer, string? version, string? reason)
    {
        string message = GlFailureMessage.Build(renderer, version, reason);
        _vm.Warning = "OpenGL 4.1 is not available. The 3D view is off.";
        _vm.IsLoading = false;
        _control.Dispatcher.BeginInvoke(() =>
            MessageBox.Show(message, "Universe3D", MessageBoxButton.OK, MessageBoxImage.Error));
    }
}
