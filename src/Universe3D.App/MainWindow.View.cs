using System.Globalization;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Universe3D.Rendering;

namespace Universe3D.App;

/// <summary>
/// Scripted start views for visual checks: <c>--view "key=value;..."</c>. Keys: <c>dataset</c> (index in the
/// list box), <c>frame</c> (Supergalactic, Galactic or Equatorial), <c>goto</c> (an object's name, other name or
/// id; it is also selected), <c>dist</c> (Mpc), <c>yaw</c> and <c>pitch</c> (degrees), <c>fromsun</c> (look along
/// the Sun's line of sight to the object), <c>shapes</c>, <c>names</c>, <c>milkyway</c>, <c>rings</c>,
/// <c>arrows</c> (on or off), <c>arrowmode</c> (observed or peculiar), <c>color</c> (type or velocity), and
/// <c>search</c> (types the text into the search box).
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
        var f = _vm.Filters;
        if (_view.TryGetValue("frame", out var frame)) f.Frame = Enum.Parse<DisplayFrame>(frame, ignoreCase: true);
        if (_view.TryGetValue("color", out var color)) f.ColorMode = Enum.Parse<ColorMode>(color, ignoreCase: true);
        bool On(string key, bool fallback) => _view.TryGetValue(key, out var v) ? v != "off" : fallback;
        f.ShowShapes = On("shapes", f.ShowShapes) && On("spheres", true);
        f.ShowNames = On("names", f.ShowNames);
        f.ShowMilkyWay = On("milkyway", f.ShowMilkyWay);
        f.ShowRings = On("rings", f.ShowRings);
        _scene.StartView = TryApplyView;
        return _view.TryGetValue("dataset", out var ds) ? int.Parse(ds, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>Points the camera as <c>--view</c> asks, once, for the first data set shown.</summary>
    private bool TryApplyView(DataSet ds)
    {
        if (_view == null) return false;
        var view = _view;
        _view = null;
        var f = _vm.Filters;
        // After Configure (which turns arrows off, Ruling R20).
        if (view.TryGetValue("arrows", out var arrows)) f.ShowArrows = arrows != "off";
        if (view.TryGetValue("arrowmode", out var mode))
            f.ArrowMode = mode.StartsWith("pec", StringComparison.OrdinalIgnoreCase) ? ArrowMode.PeculiarVelocity : ArrowMode.Observed;
        double Num(string key, double fallback) =>
            view.TryGetValue(key, out var s) ? double.Parse(s, CultureInfo.InvariantCulture) : fallback;

        var camera = _scene.Camera;
        var target = new Vector3d(0, 0, 0);
        if (view.TryGetValue("goto", out var name))
        {
            var o = ds.Objects.FirstOrDefault(o =>
                        string.Equals(o.Id, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        o.AltNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"--view: no object named '{name}'.");
            target = _scene.ToDisplay(o.PositionEq);
            _vm.Select(o, flyTo: false);
        }
        if (view.ContainsKey("fromsun") && target.Length > 0)
        {
            // Backward = −(Sun-to-object direction): the eye sits on the Sun's side of the object.
            var b = -target.Normalized();
            camera.YawDeg = Math.Atan2(b.Y, b.X) * 180 / Math.PI;
            camera.PitchDeg = Math.Asin(b.Z) * 180 / Math.PI;
        }
        else
        {
            camera.YawDeg = Num("yaw", camera.YawDeg);
            camera.PitchDeg = Num("pitch", camera.PitchDeg);
        }
        camera.FlyTo(target, Num("dist", Controllers.SceneController.FramingDistance(ds)), 0);
        if (view.TryGetValue("search", out var text))
            Dispatcher.BeginInvoke(() =>
            {
                SearchBox.Focus();
                _vm.SearchText = text;
            });
        return true;
    }
}
