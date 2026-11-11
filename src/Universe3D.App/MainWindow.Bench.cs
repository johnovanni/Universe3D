using System.Globalization;
using System.IO;
using System.Windows;
using Universe3D.Core.Objects;

namespace Universe3D.App;

/// <summary>
/// Perf measurement: run with <c>--bench &lt;csv&gt;</c>. For each configuration the camera spins continuously
/// (so every frame really renders), a warm-up passes, then frame intervals are collected for a few seconds.
/// </summary>
public partial class MainWindow
{
    private const double WarmupSeconds = 2.0, MeasureSeconds = 6.0;

    private static readonly (string Label, int Count)[] BenchLimits =
    {
        ("100000", 100_000), ("250000", 250_000), ("All", int.MaxValue),
    };

    private void StartBenchIfRequested()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--bench");
        if (i < 0 || i + 1 >= args.Length) return;
        _vm.PreloadOtherDataSets = false;
        // --bench-at X,Y moves the window first (to measure on another monitor), in device-independent pixels.
        int at = Array.IndexOf(args, "--bench-at");
        if (at >= 0 && at + 1 < args.Length)
        {
            var xy = args[at + 1].Split(',');
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = double.Parse(xy[0], CultureInfo.InvariantCulture);
            Top = double.Parse(xy[1], CultureInfo.InvariantCulture);
        }
        _ = RunBenchAsync(args[i + 1]);
    }

    private async Task RunBenchAsync(string csv)
    {
        var rows = new List<string> { "dataset,limit,arrows,objects,shapes,median_fps,median_ms,p10_fps,renderer,window_left_top" };
        // (data-set index, limit index in BenchLimits, camera distance in Mpc or 0 for the framing distance).
        // The zoomed rows put galaxy shapes on screen; at 0.03 Mpc the Milky Way and Magellanic Clouds fill the view.
        var configs = new (int ds, int limit, double zoomMpc)[]
        {
            (0, 1, 0), (1, 1, 0), (2, 1, 0), (3, 0, 0), (3, 1, 0), (3, 2, 0), (0, 1, 0.5), (0, 1, 0.03), (1, 1, 5),
        };
        _scene.Spin = true;
        try
        {
            while (!_scene.IsReady) await Task.Delay(100);
            foreach (var (ds, limit, zoomMpc) in configs)
            {
                foreach (bool arrows in new[] { false, true })
                {
                    var dsvm = _vm.DataSets[ds];
                    _vm.SelectedDataSet = dsvm;
                    _vm.Filters.QuasarLimit = BenchLimits[limit].Count;
                    await Task.Delay(300);
                    while (_vm.IsLoading || _vm.Shown?.Kind != dsvm.Kind || _scene.IsBusy) await Task.Delay(100);
                    if (zoomMpc > 0) _scene.Camera.DistanceMpc = zoomMpc;
                    _vm.Filters.ShowArrows = arrows;
                    await Task.Delay((int)(WarmupSeconds * 1000));
                    _scene.Timer.Reset();
                    await Task.Delay((int)(MeasureSeconds * 1000));
                    var s = _scene.Timer.Samples.OrderBy(x => x).ToArray();
                    double med = s[s.Length / 2], p90 = s[(int)(0.9 * (s.Length - 1))];
                    bool quasars = dsvm.Kind == DataSetKind.Quasars;
                    int objects = _vm.Shown!.Objects.Count;
                    string label = zoomMpc > 0
                        ? $"{dsvm.Title} at {zoomMpc.ToString(CultureInfo.InvariantCulture)} Mpc"
                        : dsvm.Title;
                    rows.Add(string.Join(',', label.Replace(",", ""), quasars ? BenchLimits[limit].Label : "-",
                        arrows ? "on" : "off", objects.ToString(CultureInfo.InvariantCulture),
                        _scene.ShapeCount.ToString(CultureInfo.InvariantCulture),
                        (1000 / med).ToString("F1", CultureInfo.InvariantCulture),
                        med.ToString("F2", CultureInfo.InvariantCulture),
                        (1000 / p90).ToString("F1", CultureInfo.InvariantCulture),
                        _scene.GlInfo.Split('|').ElementAtOrDefault(1)?.Trim() ?? "",
                        $"{(int)Left}x{(int)Top}"));
                    File.WriteAllLines(csv, rows);
                }
            }
            rows.Add("DONE");
        }
        catch (Exception ex)
        {
            rows.Add("ERROR," + ex.Message.Replace(',', ';'));
        }
        File.WriteAllLines(csv, rows);
        Application.Current.Shutdown();
    }
}
