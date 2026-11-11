using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Universe3D.Rendering;

namespace Universe3D.App;

/// <summary>Minimal host: a data-set combo box and the GL view. A = arrows, V = color mode, wheel/drag = camera.</summary>
public partial class MainWindow : Window
{
    private DisplayFrame _frame = DisplayFrame.Supergalactic;

    private static readonly DataSetChoice[] Choices =
    {
        new(DataSetKind.LocalVolume, "Local Volume"),
        new(DataSetKind.NearbyUniverse, "Nearby universe"),
        new(DataSetKind.Clusters, "Galaxy clusters"),
        new(DataSetKind.Quasars, "Quasars"),
    };

    private static readonly LimitChoice[] Limits =
    {
        new("100,000", 100_000),
        new("250,000", 250_000),
        new("All", int.MaxValue),
    };

    private readonly IDataSetStore _store = new SnapshotStore();
    private readonly OrbitCamera _camera = new() { YawDeg = 30, PitchDeg = 20, DistanceMpc = 30 };
    private readonly GlHost _host;

    private PointCloudRenderer? _points;
    private VelocityArrowRenderer? _arrows;
    private ShapeRenderer? _shapes;
    private ClusterRenderer? _clusters;
    private MilkyWayRenderer? _milkyWay;
    private ScaleRingRenderer? _rings;
    private readonly List<TextBlock> _ringLabels = new();
    private DataSet? _pendingUpload;
    private int _loadGeneration;
    private string _status = "Starting…";
    private DataSet? _loaded;
    private DataSetKind _loadedKind;
    private bool _spin;
    private readonly FrameTimer _timer = new();
    private int _fpsTextTick;

    private Point _lastMouse;
    private MouseButton? _dragButton;
    private int _wheelDelta;

    public MainWindow()
    {
        InitializeComponent();
        _host = new GlHost(GlControl);
        _host.ContextReady += OnContextReady;
        _host.Draw += OnDraw;
        _host.FrameFailed += ex => ShowError("Rendering failed", ex);
        try
        {
            _host.Start();
        }
        catch (Exception ex)
        {
            ShowError("Could not create an OpenGL 4.1 core context", ex);
        }

        PreviewKeyDown += OnPreviewKeyDown;
        LimitCombo.ItemsSource = Limits;
        LimitCombo.SelectedIndex = 1;
        DataSetCombo.ItemsSource = Choices;
        DataSetCombo.SelectedIndex = ParseViewArgs();
        Loaded += (_, _) => StartBenchIfRequested();
    }

    private sealed record DataSetChoice(DataSetKind Kind, string Label);

    private sealed record LimitChoice(string Label, int Count);

    private void OnContextReady(string glInfo)
    {
        var buffers = new GpuBuffers();
        _points = new PointCloudRenderer(buffers);
        _arrows = new VelocityArrowRenderer(_points);
        _shapes = new ShapeRenderer(_points);
        _clusters = new ClusterRenderer(_points);
        _milkyWay = new MilkyWayRenderer();
        _milkyWay.Upload(_frame);
        _rings = new ScaleRingRenderer();
        ApplyLayers();
        UpdateTitle();
    }

    private void OnDraw(TimeSpan delta, Size viewport)
    {
        if (_points == null || _arrows == null || _shapes == null || _clusters == null || _milkyWay == null ||
            _rings == null) return;
        if (_pendingUpload != null)
        {
            _points.Upload(_pendingUpload, _frame);
            _shapes.Upload(_pendingUpload, _frame);
            _clusters.Upload(_pendingUpload, _frame);
            _pendingUpload = null;
        }
        _timer.Tick();
        double dt = Math.Min(delta.TotalSeconds, 0.1);
        _camera.Update(dt);
        if (_spin) _camera.YawDeg = (_camera.YawDeg + SpinDegPerSecond * dt) % 360;

        // Select the shapes first: the points fade out where the shapes fade in.
        _shapes.Update(_camera, viewport);
        _points.ShapeFadePx = _shapes.FadeRangePx;
        _points.GrowWithDiameter = !(_clusters.Visible && _clusters.Count > 0);

        _rings.Render(_camera, viewport);
        _milkyWay.Render(_camera, viewport);
        _clusters.Render(_camera, viewport);
        _points.Render(_camera, viewport);
        _shapes.Render(_camera, viewport);
        _arrows.Render(_camera, viewport);
        UpdateRingLabels(viewport);
        _host.Animating = _camera.IsFlying || _spin;
        if (++_fpsTextTick % 30 == 0) UpdateFpsText();
    }

    private void Layer_Changed(object sender, RoutedEventArgs e)
    {
        if (_points == null) return; // still initializing
        ApplyLayers();
        UpdateTitle();
        _host.RequestRender();
    }

    private void ApplyLayers()
    {
        if (_shapes != null) _shapes.Visible = ShapesCheck.IsChecked == true;
        if (_clusters != null) _clusters.Visible = SpheresCheck.IsChecked == true;
        if (_milkyWay != null) _milkyWay.Visible = MilkyWayCheck.IsChecked == true;
        if (_rings != null) _rings.Visible = RingsCheck.IsChecked == true;
    }

    /// <summary>Places the ring labels; the renderer reports framebuffer pixels, the overlay uses DIPs.</summary>
    private void UpdateRingLabels(Size viewport)
    {
        var labels = _rings?.Labels ?? Array.Empty<RingLabel>();
        double scale = viewport.Width > 0 ? GlControl.ActualWidth / viewport.Width : 1;
        while (_ringLabels.Count < labels.Count)
        {
            var tb = new TextBlock { Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x8C, 0xAE, 0xDA)), FontSize = 12 };
            _ringLabels.Add(tb);
            Overlay.Children.Add(tb);
        }
        for (int i = 0; i < _ringLabels.Count; i++)
        {
            var tb = _ringLabels[i];
            if (i >= labels.Count) { tb.Visibility = Visibility.Collapsed; continue; }
            tb.Text = labels[i].Text;
            tb.Visibility = Visibility.Visible;
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double x = labels[i].ScreenPx.X * scale + 4, y = labels[i].ScreenPx.Y * scale + 2;
            Canvas.SetLeft(tb, Math.Min(x, Overlay.ActualWidth - tb.DesiredSize.Width - 4));
            Canvas.SetTop(tb, Math.Min(y, Overlay.ActualHeight - tb.DesiredSize.Height - 2));
        }
    }

    private const double SpinDegPerSecond = 45;

    private void UpdateFpsText()
    {
        if (_timer.Count < 5) { FpsText.Text = ""; return; }
        double ms = _timer.MedianMs;
        string renderer = _host.GlInfo.Split('|').ElementAtOrDefault(1)?.Trim() ?? "";
        var cls = GpuClassifier.Classify(renderer, ms);
        FpsText.Text = $"{1000 / ms:F0} fps  {ms:F1} ms  ({cls})  shapes {_shapes?.ShapeCount ?? 0}";
    }

    private void SpinCheck_Changed(object sender, RoutedEventArgs e)
    {
        _spin = SpinCheck.IsChecked == true;
        _timer.Reset();
        _host.Animating = _spin || _camera.IsFlying;
        _host.RequestRender();
    }

    private void LimitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyLimit();

    /// <summary>The quasar limit box: shows only the first N objects (snapshots are sorted brightest first).</summary>
    private void ApplyLimit()
    {
        if (_loaded == null || LimitCombo.SelectedItem is not LimitChoice limit) return;
        _pendingUpload = _loadedKind == DataSetKind.Quasars ? Limited(_loaded, limit.Count) : _loaded;
        _host.RequestRender();
    }

    private static DataSet Limited(DataSet ds, int count) =>
        count >= ds.Objects.Count
            ? ds
            : new DataSet(ds.Kind, ds.Objects.Take(count).ToList(), ds.ExcludedCount, ds.SkippedRows,
                          ds.BuiltUtc, ds.SourceSummary);

    private async void DataSetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataSetCombo.SelectedItem is not DataSetChoice choice) return;
        int generation = ++_loadGeneration;
        _status = $"Loading {choice.Label}…";
        UpdateTitle();
        try
        {
            var ds = await Task.Run(() => _store.Load(choice.Kind, out _));
            if (generation != _loadGeneration) return;
            _loaded = ds;
            _loadedKind = choice.Kind;
            _timer.Reset();
            var limit = LimitCombo.SelectedItem as LimitChoice;
            _pendingUpload = choice.Kind == DataSetKind.Quasars && limit != null ? Limited(ds, limit.Count) : ds;
            _status = $"{choice.Label}: {ds.Objects.Count.ToString("N0", CultureInfo.InvariantCulture)} objects";
            if (!TryApplyView(ds)) _camera.FlyTo(new Vector3d(0, 0, 0), FramingDistance(ds));
            _host.Animating = true;
            _host.RequestRender();
        }
        catch (Exception ex)
        {
            ShowError($"Could not load {choice.Label}", ex);
        }
        UpdateTitle();
    }

    /// <summary>Far enough back to see most of the data set around the Sun.</summary>
    private static double FramingDistance(DataSet ds)
    {
        var d = ds.Objects.Select(o => o.DistanceMpc).Where(double.IsFinite).OrderBy(x => x).ToArray();
        if (d.Length == 0) return 30;
        return 2.2 * d[(int)(0.95 * (d.Length - 1))];
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_points == null || _arrows == null) return;
        switch (e.Key)
        {
            case Key.A:
                _arrows.Visible = !_arrows.Visible;
                break;
            case Key.V:
                _points.ColorMode = _points.ColorMode == ColorMode.Type ? ColorMode.Velocity : ColorMode.Type;
                break;
            default:
                return;
        }
        e.Handled = true;
        UpdateTitle();
        _host.RequestRender();
    }

    private void Gl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragButton = e.ChangedButton;
        _lastMouse = e.GetPosition(GlControl);
        GlControl.CaptureMouse();
    }

    private void Gl_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragButton is not { } button) return;
        var p = e.GetPosition(GlControl);
        double dx = p.X - _lastMouse.X, dy = p.Y - _lastMouse.Y;
        _lastMouse = p;
        bool pan = button != MouseButton.Left || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (pan) _camera.Pan(dx, dy, GlControl.ActualHeight);
        else _camera.Rotate(dx, dy);
        _host.RequestRender();
    }

    private void Gl_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != _dragButton) return;
        _dragButton = null;
        GlControl.ReleaseMouseCapture();
    }

    private void Gl_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Touchpads send fractions of a notch; zoom once a whole notch has accumulated.
        _wheelDelta += e.Delta;
        int notches = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        if (notches == 0) return;
        _wheelDelta -= notches * Mouse.MouseWheelDeltaForOneLine;
        _camera.Zoom(notches);
        _host.RequestRender();
    }

    private void UpdateTitle()
    {
        string arrows = _arrows == null ? "" : _arrows.Visible ? " | arrows on (A)" : " | arrows off (A)";
        string color = _points == null ? "" : $" | color: {_points.ColorMode} (V)";
        Title = $"Universe3D | {_status}{arrows}{color} | {_host.GlInfo}";
    }

    private void ShowError(string what, Exception ex)
    {
        _status = $"{what}: {ex.Message}";
        UpdateTitle();
        Dispatcher.BeginInvoke(() =>
            MessageBox.Show($"{what}.\n\n{ex}", "Universe3D", MessageBoxButton.OK, MessageBoxImage.Error));
    }
}
