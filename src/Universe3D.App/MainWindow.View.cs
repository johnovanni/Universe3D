using System.Globalization;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Objects;

namespace Universe3D.App;

/// <summary>
/// Scripted start views for visual checks: <c>--view "key=value;..."</c>. Keys: <c>dataset</c> (index in the
/// combo box), <c>frame</c> (Supergalactic, Galactic or Equatorial), <c>goto</c> (an object's name, other name or
/// id), <c>dist</c> (Mpc), <c>yaw</c> and <c>pitch</c> (degrees), <c>fromsun</c> (look along the Sun's line of
/// sight to the object), and <c>shapes</c>, <c>spheres</c>, <c>milkyway</c>, <c>rings</c>, <c>arrows</c> (on or off).
/// </summary>
public partial class MainWindow
{
    private Dictionary<string, string>? _view;

    /// <summary>Reads <c>--view</c>; returns the data-set index to load first.</summary>
    private int ParseViewArgs()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--view");
        if (i < 0 || i + 1 >= args.Length) return 0;
        _view = args[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(kv => kv.Split('=', 2))
            .ToDictionary(kv => kv[0].ToLowerInvariant(), kv => kv.Length > 1 ? kv[1] : "on");
        if (_view.TryGetValue("frame", out var frame)) _frame = Enum.Parse<DisplayFrame>(frame, ignoreCase: true);
        foreach (var (key, box) in new[] { ("shapes", ShapesCheck), ("spheres", SpheresCheck),
                                           ("milkyway", MilkyWayCheck), ("rings", RingsCheck) })
            if (_view.TryGetValue(key, out var on)) box.IsChecked = on != "off";
        return _view.TryGetValue("dataset", out var ds) ? int.Parse(ds, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>Points the camera as <c>--view</c> asks, once, for the first data set loaded.</summary>
    private bool TryApplyView(DataSet ds)
    {
        if (_view == null) return false;
        var view = _view;
        _view = null;
        if (_arrows != null && view.TryGetValue("arrows", out var arrows)) _arrows.Visible = arrows != "off";
        double Num(string key, double fallback) =>
            view.TryGetValue(key, out var s) ? double.Parse(s, CultureInfo.InvariantCulture) : fallback;

        var target = new Vector3d(0, 0, 0);
        if (view.TryGetValue("goto", out var name))
        {
            var o = ds.Objects.FirstOrDefault(o =>
                        string.Equals(o.Id, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        o.AltNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"--view: no object named '{name}'.");
            target = SkyFrames.EquatorialTo(_frame) * o.PositionEq;
        }
        if (view.ContainsKey("fromsun") && target.Length > 0)
        {
            // Backward = −(Sun-to-object direction): the eye sits on the Sun's side of the object.
            var b = -target.Normalized();
            _camera.YawDeg = Math.Atan2(b.Y, b.X) * 180 / Math.PI;
            _camera.PitchDeg = Math.Asin(b.Z) * 180 / Math.PI;
        }
        else
        {
            _camera.YawDeg = Num("yaw", _camera.YawDeg);
            _camera.PitchDeg = Num("pitch", _camera.PitchDeg);
        }
        _camera.FlyTo(target, Num("dist", FramingDistance(ds)), 0);
        return true;
    }
}
