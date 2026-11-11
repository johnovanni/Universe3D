using System.Windows;
using System.Windows.Controls;
using OpenTK.Wpf;
using Universe3D.App.Overlay;
using Universe3D.App.ViewModels;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;
using Universe3D.Rendering;

namespace Universe3D.App.Controllers;

/// <summary>
/// Draws what the <see cref="MainViewModel"/> describes: uploads the shown data set (again when the display frame
/// changes, Milky Way included), applies filters and toggles to the renderers each frame, moves the camera (Home,
/// F, fly-to), and places labels. Input and picking are in SceneController.Input.cs, labels in
/// SceneController.Labels.cs.
/// </summary>
public sealed partial class SceneController : IDisposable
{
    private const double SpinDegPerSecond = 45;
    private const int StatusEveryFrames = 15;

    private readonly GLWpfControl _control;
    private readonly MainViewModel _vm;
    private readonly GlHost _host;
    private readonly LabelOverlay _overlay;

    private GpuBuffers? _buffers;
    private PointCloudRenderer? _points;
    private VelocityArrowRenderer? _arrows;
    private ShapeRenderer? _shapes;
    private ClusterRenderer? _clusters;
    private MilkyWayRenderer? _milkyWay;
    private ScaleRingRenderer? _rings;

    private DataSet? _uploaded;
    private DisplayFrame? _uploadedFrame;
    private Matrix3d _toDisplay = Matrix3d.Identity;
    private Size _viewportPx;
    private bool _spin;
    private int _frames;
    private bool _disposed;

    public SceneController(GLWpfControl control, Canvas overlay, MainViewModel vm)
    {
        _control = control;
        _vm = vm;
        _overlay = new LabelOverlay(overlay);
        _host = new GlHost(control);
        _host.ContextReady += OnContextReady;
        _host.Draw += OnDraw;
        _host.FrameFailed += ex =>
        {
            if (ex is GlUnsupportedException gl) ShowGlFailure(gl.Renderer, gl.Version, null);
            else ShowError("Rendering stopped", ex);
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.Shown) or nameof(MainViewModel.Selected)) RequestRender();
        };
        vm.Filters.PropertyChanged += (_, _) => RequestRender();
        vm.FlyToRequested += FlyTo;
        vm.HomeRequested += Home;
        vm.CenterRequested += CenterOnSelected;
        control.SizeChanged += (_, _) => RequestRender();
        AttachInput();
    }

    public OrbitCamera Camera { get; } = new() { YawDeg = 30, PitchDeg = 20, DistanceMpc = 30 };

    /// <summary>Frame intervals while something animates (for the frame rate and --bench).</summary>
    public FrameTimer Timer { get; } = new();

    public string GlInfo => _host.GlInfo;

    public int ShapeCount => _shapes?.ShapeCount ?? 0;

    /// <summary>True once the GL context and renderers exist.</summary>
    public bool IsReady => _points != null;

    /// <summary>True while the shown data set or frame waits for the next frame, or the camera is flying.</summary>
    public bool IsBusy => _vm.Shown != _uploaded || _vm.Filters.Frame != _uploadedFrame || Camera.IsFlying;

    /// <summary>Turns the camera continuously (for --bench, so every frame really renders).</summary>
    public bool Spin
    {
        get => _spin;
        set
        {
            _spin = value;
            Timer.Reset();
            _host.Animating = value || Camera.IsFlying;
            RequestRender();
        }
    }

    /// <summary>
    /// Called instead of <see cref="Home"/> when a data set of a new kind is first shown (--view); returns true when
    /// it placed the camera.
    /// </summary>
    public Func<DataSet, bool>? StartView { get; set; }

    /// <summary>An equatorial position in the current display frame.</summary>
    public Vector3d ToDisplay(Vector3d equatorial) => SkyFrames.EquatorialTo(_vm.Filters.Frame) * equatorial;

    /// <summary>Creates the OpenGL 4.1 context; shows a message (and keeps the window) if that fails.</summary>
    public void Start()
    {
        try
        {
            _host.Start();
        }
        catch (Exception ex)
        {
            ShowGlFailure(null, null, ex.Message);
        }
    }

    public void RequestRender()
    {
        if (!_disposed) _host.RequestRender();
    }

    /// <summary>Home: fly back to the Sun, far enough out to see most of the shown objects.</summary>
    public void Home()
    {
        if (_vm.Shown is not { } ds) return;
        Camera.FlyTo(new Vector3d(0, 0, 0), FramingDistance(ds, _vm.Filters.MaxDistanceMpc));
        StartAnimation();
    }

    /// <summary>F: center on the selection, keeping the zoom.</summary>
    public void CenterOnSelected()
    {
        if (_vm.Selected is not { } o) return;
        Camera.FlyTo(ToDisplay(o.PositionEq), Camera.DistanceMpc);
        StartAnimation();
    }

    /// <summary>Flies to an object and zooms to a distance that suits its size.</summary>
    public void FlyTo(CosmicObject o)
    {
        Camera.FlyTo(ToDisplay(o.PositionEq), FocusDistanceMpc(o));
        StartAnimation();
    }

    /// <summary>2.2 × the 95th-percentile distance of the objects within the distance filter.</summary>
    public static double FramingDistance(DataSet ds, double maxDistanceMpc = double.PositiveInfinity)
    {
        var d = ds.Objects.Select(o => o.DistanceMpc).Where(x => double.IsFinite(x) && x <= maxDistanceMpc)
                  .OrderBy(x => x).ToArray();
        if (d.Length == 0) return 30;
        return 2.2 * d[(int)(0.95 * (d.Length - 1))];
    }

    /// <summary>A viewing distance that shows the object: a few diameters for galaxies and clusters.</summary>
    public static double FocusDistanceMpc(CosmicObject o) => o switch
    {
        Galaxy { DiameterKpc: double kpc } when kpc > 0 => Math.Max(3 * kpc / 1000, 2e-3),
        Galaxy => 0.05,
        GalaxyCluster c => 6 * ClusterRenderer.RadiusMpc(c),
        _ => Math.Max(5, 0.05 * o.DistanceMpc),
    };

    private void StartAnimation()
    {
        _host.Animating = true;
        RequestRender();
    }

    private void OnContextReady(string glInfo)
    {
        RequireOpenGl41(glInfo);
        _buffers = new GpuBuffers();
        _points = new PointCloudRenderer(_buffers);
        _arrows = new VelocityArrowRenderer(_points);
        _shapes = new ShapeRenderer(_points);
        _clusters = new ClusterRenderer(_points);
        _milkyWay = new MilkyWayRenderer();
        _rings = new ScaleRingRenderer();
        BeginGpuClassification(glInfo);
    }

    private void OnDraw(TimeSpan delta, Size viewport)
    {
        if (_points == null || _arrows == null || _shapes == null || _clusters == null || _milkyWay == null ||
            _rings == null) return;
        _viewportPx = viewport;
        SyncUploads(_points, _shapes, _clusters, _milkyWay);
        SyncOptions(_points, _arrows, _shapes, _clusters, _milkyWay, _rings);

        if (_host.Animating || _dragButton != null) Timer.Tick();
        else Timer.Reset();
        double dt = Math.Min(delta.TotalSeconds, 0.1);
        Camera.Update(dt);
        if (_spin) Camera.YawDeg = (Camera.YawDeg + SpinDegPerSecond * dt) % 360;

        // Select the shapes first: the points fade out where the shapes fade in.
        _shapes.Update(Camera, viewport);
        _points.ShapeFadePx = _shapes.FadeRangePx;
        _points.GrowWithDiameter = !(_clusters.Visible && _clusters.Count > 0);

        _rings.Render(Camera, viewport);
        _milkyWay.Render(Camera, viewport);
        _clusters.Render(Camera, viewport);
        _points.Render(Camera, viewport);
        _shapes.Render(Camera, viewport);
        _arrows.Render(Camera, viewport);
        UpdateOverlay(viewport);

        CalibrationStep();
        _host.Animating = Camera.IsFlying || _spin || Calibrating;
        if (++_frames % StatusEveryFrames == 0 || !_host.Animating) UpdateStatus();
    }

    /// <summary>
    /// Uploads what changed: a new shown data set, or a new display frame. A frame change re-uploads every
    /// frame-dependent layer (points, shapes, clusters and the Milky Way) and keeps the camera on the same spot.
    /// </summary>
    private void SyncUploads(PointCloudRenderer points, ShapeRenderer shapes, ClusterRenderer clusters,
                             MilkyWayRenderer milkyWay)
    {
        var frame = _vm.Filters.Frame;
        bool frameChanged = frame != _uploadedFrame;
        if (frameChanged)
        {
            var toDisplay = SkyFrames.EquatorialTo(frame);
            if (_uploadedFrame != null)
                Camera.FlyTo(toDisplay * (_toDisplay.Transpose() * Camera.Target), Camera.DistanceMpc, 0);
            _toDisplay = toDisplay;
            _uploadedFrame = frame;
            milkyWay.Upload(frame);
        }

        var shown = _vm.Shown;
        if (shown == null || (shown == _uploaded && !frameChanged)) return;
        points.Upload(shown, frame);
        shapes.Upload(shown, frame);
        clusters.Upload(shown, frame);
        if (shown == _uploaded) return;

        var previous = _uploaded;
        _uploaded = shown;
        OnDataShown(shown);
        if (previous?.Kind != shown.Kind && StartView?.Invoke(shown) != true) Home();
    }

    private void SyncOptions(PointCloudRenderer points, VelocityArrowRenderer arrows, ShapeRenderer shapes,
                             ClusterRenderer clusters, MilkyWayRenderer milkyWay, ScaleRingRenderer rings)
    {
        var f = _vm.Filters;
        points.MaxDistanceMpc = f.MaxDistanceMpc;
        points.TypeMask = f.TypeMask;
        points.ColorMode = f.ColorMode;
        shapes.Visible = f.ShowShapes;
        shapes.Cap = f.ShapeCap;
        clusters.Visible = f.ShowShapes;
        milkyWay.Visible = f.ShowMilkyWay;
        rings.Visible = f.ShowRings;
        arrows.Visible = f.ShowArrows;
        arrows.Mode = f.ArrowMode;
        arrows.Scale = f.ArrowScale;
    }

    private void UpdateStatus()
    {
        _vm.CameraStatus = "View distance " + Units.FormatDistance(Camera.DistanceMpc);
        if (Timer.Count >= 5) _vm.FrameRateStatus = $"{1000 / Timer.MedianMs:F0} fps";
    }

    private void ShowError(string what, Exception ex)
    {
        _vm.Warning = $"{what}: {ex.Message}";
        _control.Dispatcher.BeginInvoke(() =>
            MessageBox.Show($"{what}.\n\n{ex.Message}", "Universe3D", MessageBoxButton.OK, MessageBoxImage.Error));
    }

    /// <summary>Deletes every GL object with the context current. Call when the window closes.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Shutdown(() =>
        {
            _arrows?.Dispose();
            _shapes?.Dispose();
            _clusters?.Dispose();
            _milkyWay?.Dispose();
            _rings?.Dispose();
            _points?.Dispose();
            _buffers?.Dispose();
        });
        _arrows = null;
        _shapes = null;
        _clusters = null;
        _milkyWay = null;
        _rings = null;
        _points = null;
        _buffers = null;
    }
}
