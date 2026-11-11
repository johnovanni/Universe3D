using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Objects;
using Universe3D.Rendering;

namespace Universe3D.App;

/// <summary>Minimal host: a data-set combo box and the GL view. A = arrows, V = color mode, wheel/drag = camera.</summary>
public partial class MainWindow : Window
{
    private const DisplayFrame Frame = DisplayFrame.Supergalactic;

    private static readonly DataSetChoice[] Choices =
    {
        new(DataSetKind.LocalVolume, "Local Volume"),
        new(DataSetKind.NearbyUniverse, "Nearby universe"),
        new(DataSetKind.Clusters, "Galaxy clusters"),
        new(DataSetKind.Quasars, "Quasars"),
    };

    private readonly IDataSetStore _store = new SnapshotStore();
    private readonly OrbitCamera _camera = new() { YawDeg = 30, PitchDeg = 20, DistanceMpc = 30 };
    private readonly GlHost _host;

    private PointCloudRenderer? _points;
    private VelocityArrowRenderer? _arrows;
    private DataSet? _pendingUpload;
    private int _loadGeneration;
    private string _status = "Starting…";

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
        DataSetCombo.ItemsSource = Choices;
        DataSetCombo.SelectedIndex = 0;
    }

    private sealed record DataSetChoice(DataSetKind Kind, string Label);

    private void OnContextReady(string glInfo)
    {
        var buffers = new GpuBuffers();
        _points = new PointCloudRenderer(buffers);
        _arrows = new VelocityArrowRenderer(_points);
        UpdateTitle();
    }

    private void OnDraw(TimeSpan delta, Size viewport)
    {
        if (_points == null || _arrows == null) return;
        if (_pendingUpload != null)
        {
            _points.Upload(_pendingUpload, Frame);
            _pendingUpload = null;
        }
        _camera.Update(Math.Min(delta.TotalSeconds, 0.1));
        _points.Render(_camera, viewport);
        _arrows.Render(_camera, viewport);
        _host.Animating = _camera.IsFlying;
    }

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
            _pendingUpload = ds;
            _status = $"{choice.Label}: {ds.Objects.Count.ToString("N0", CultureInfo.InvariantCulture)} objects";
            _camera.FlyTo(new Vector3d(0, 0, 0), FramingDistance(ds));
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
