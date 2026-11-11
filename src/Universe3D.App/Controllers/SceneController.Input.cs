using System.Windows;
using System.Windows.Input;
using Universe3D.Core.Objects;
using Universe3D.Rendering;

namespace Universe3D.App.Controllers;

/// <summary>Mouse input: left-drag rotates, right-drag (or Shift+left) pans, the wheel zooms, a click selects and a
/// double-click flies to an object.</summary>
public sealed partial class SceneController
{
    /// <summary>The picking radius (8 px), in WPF device-independent pixels.</summary>
    private const double PickRadiusDip = 8;

    /// <summary>A press that moves less than this is a click, not a drag.</summary>
    private const double ClickSlopDip = 4;

    private Point _lastMouse, _downAt;
    private MouseButton? _dragButton;
    private bool _dragged;
    private int _wheelDelta;

    private void AttachInput()
    {
        _control.MouseDown += OnMouseDown;
        _control.MouseMove += OnMouseMove;
        _control.MouseUp += OnMouseUp;
        _control.MouseWheel += OnMouseWheel;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_control);
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            if (PickAt(p) is { } o) _vm.Select(o, flyTo: true);
            e.Handled = true;
            return;
        }
        _dragButton = e.ChangedButton;
        _downAt = _lastMouse = p;
        _dragged = false;
        _control.CaptureMouse();
        _control.Focus();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragButton is not { } button) return;
        var p = e.GetPosition(_control);
        if (!_dragged && (p - _downAt).Length < ClickSlopDip) return;
        _dragged = true;
        double dx = p.X - _lastMouse.X, dy = p.Y - _lastMouse.Y;
        _lastMouse = p;
        bool pan = button != MouseButton.Left || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (pan) Camera.Pan(dx, dy, _control.ActualHeight);
        else Camera.Rotate(dx, dy);
        RequestRender();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != _dragButton) return;
        _dragButton = null;
        _control.ReleaseMouseCapture();
        if (!_dragged && e.ChangedButton == MouseButton.Left && PickAt(e.GetPosition(_control)) is { } o)
            _vm.Select(o, flyTo: false);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Touchpads send fractions of a notch; zoom once a whole notch has accumulated.
        _wheelDelta += e.Delta;
        int notches = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        if (notches == 0) return;
        _wheelDelta -= notches * Mouse.MouseWheelDeltaForOneLine;
        Camera.Zoom(notches);
        RequestRender();
    }

    /// <summary>
    /// The object under a mouse position (WPF device-independent pixels). The GL framebuffer is in physical pixels,
    /// so the click and the radius are scaled by the display scaling (1.25 at 125%, 1.5 at 150%) first.
    /// </summary>
    public CosmicObject? PickAt(Point dip)
    {
        if (_uploaded is not { } ds || _points == null || _viewportPx.Width <= 0 || _control.ActualWidth <= 0)
            return null;
        double pxPerDip = _viewportPx.Width / _control.ActualWidth;
        var click = new Point(dip.X * pxPerDip, dip.Y * pxPerDip);
        var proj = new Projector(Camera, _viewportPx.Width, _viewportPx.Height);
        int i = ShapeUnder(ds, proj, click);
        if (i < 0) i = Picker.Pick(_points.Positions, _points.IsVisible, proj, click, PickRadiusDip * pxPerDip);
        return i >= 0 ? ds.Objects[i] : null;
    }

    /// <summary>
    /// Shapes under the cursor win over nearby points (spec §4). Of several, the smallest on screen wins, so a
    /// satellite drawn over a large disk stays clickable. A shape counts once it is drawn (wider than 4 px); its
    /// outline is approximated by a circle of its projected diameter.
    /// </summary>
    private int ShapeUnder(DataSet ds, Projector proj, Point click)
    {
        if (_points == null || !_vm.Filters.ShowShapes) return -1;
        int best = -1;
        double bestPx = double.MaxValue;
        var positions = _points.Positions;
        for (int i = 0; i < ds.Objects.Count; i++)
        {
            double diameterMpc = ds.Objects[i] switch
            {
                Galaxy { DiameterKpc: double kpc } when kpc > 0 => kpc / 1000,
                GalaxyCluster c => 2 * ClusterRenderer.RadiusMpc(c),
                _ => 0,
            };
            if (diameterMpc <= 0) continue;
            double px = proj.PixelSize(diameterMpc, positions[i]);
            if (px <= ShapeRenderer.FadeStartPx || px >= bestPx || !_points.IsVisible(i)) continue;
            if (!proj.TryProject(positions[i], out var s)) continue;
            double dx = s.X - click.X, dy = s.Y - click.Y;
            if (dx * dx + dy * dy > px * px / 4) continue;
            best = i;
            bestPx = px;
        }
        return best;
    }
}
