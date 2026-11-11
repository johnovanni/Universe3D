using System.Windows;
using System.Windows.Media;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Universe3D.Rendering;

namespace Universe3D.App.Controllers;

/// <summary>
/// The 2D overlay: scale-ring labels, the selection ring, and up to 60 name labels in priority order (spec §4):
/// the selected object, the Milky Way, then <see cref="LabelPriority"/>.
/// </summary>
public sealed partial class SceneController
{
    public const int MaxLabels = 60;

    private static readonly Color LabelColor = Color.FromRgb(0xC8, 0xD0, 0xE0);
    private static readonly Color MilkyWayColor = Color.FromRgb(0xFF, 0xE8, 0xB0);
    private static readonly Color RingColor = Color.FromRgb(0x8C, 0xAE, 0xDA);

    private int[] _labelOrder = Array.Empty<int>();
    private Dictionary<CosmicObject, int> _indexOf = new(ReferenceEqualityComparer.Instance);

    private void OnDataShown(DataSet ds)
    {
        _labelOrder = LabelPriority.Order(ds.Objects);
        _indexOf = new Dictionary<CosmicObject, int>(ds.Objects.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < ds.Objects.Count; i++) _indexOf[ds.Objects[i]] = i;
    }

    /// <summary>The renderers work in framebuffer pixels; the overlay canvas in device-independent pixels.</summary>
    private void UpdateOverlay(Size viewport)
    {
        if (_points == null || _rings == null) return;
        double dipPerPx = _control.ActualWidth > 0 ? _control.ActualWidth / viewport.Width : 1;
        Point Dip(Point px) => new(px.X * dipPerPx, px.Y * dipPerPx);
        var proj = new Projector(Camera, viewport.Width, viewport.Height);
        var f = _vm.Filters;

        _overlay.Begin();
        int placed = 0, selected = -1;
        Point? ringAt = null;
        if (_vm.Selected is { } sel && _indexOf.TryGetValue(sel, out int si) &&
            _points.IsVisible(si) && proj.TryProject(_points.Positions[si], out var sp))
        {
            selected = si;
            ringAt = Dip(sp);
            _overlay.TryAdd(sel.Name, ringAt.Value, Overlay.LabelOverlay.SelectionColor, force: true, fontSize: 13);
            placed++;
        }
        _overlay.ShowSelection(ringAt);
        if (f.ShowNames && f.ShowMilkyWay && proj.TryProject(_toDisplay * MilkyWayModel.CenterEq, out var mw) &&
            _overlay.TryAdd("Milky Way", Dip(mw), MilkyWayColor))
            placed++;

        double overlayWidth = _control.ActualWidth;
        foreach (var ring in _rings.Labels)
        {
            // Keep the label inside the view (the overlay draws it to the right of its point).
            var at = Dip(ring.ScreenPx);
            at.X = Math.Min(at.X, overlayWidth - ring.Text.Length * 12 * 0.55 - 16);
            _overlay.TryAdd(ring.Text, at, RingColor, force: true);
        }

        if (f.ShowNames && _uploaded is { } ds)
        {
            var positions = _points.Positions;
            foreach (int i in _labelOrder)
            {
                if (placed >= MaxLabels) break;
                if (i == selected || !_points.IsVisible(i)) continue;
                if (!proj.TryProject(positions[i], out var p) ||
                    p.X < 0 || p.Y < 0 || p.X > viewport.Width || p.Y > viewport.Height) continue;
                if (_overlay.TryAdd(ds.Objects[i].Name, Dip(p), LabelColor)) placed++;
            }
        }
        _overlay.End();
    }
}
