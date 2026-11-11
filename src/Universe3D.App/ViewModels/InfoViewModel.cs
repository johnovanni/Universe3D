using CommunityToolkit.Mvvm.ComponentModel;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;

namespace Universe3D.App.ViewModels;

/// <summary>An information-panel row with its glossary tooltip (null when the label is not an astronomy term).</summary>
public sealed record InfoRowViewModel(string Label, string Value, string? ToolTip);

/// <summary>The right panel: the selected object's name, kind and <see cref="ObjectInfoFormatter"/> rows.</summary>
public sealed partial class InfoViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Nothing selected";
    [ObservableProperty] private string _kindText = "Click an object, or search for one by name.";
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private IReadOnlyList<InfoRowViewModel> _rows = Array.Empty<InfoRowViewModel>();

    public void Show(CosmicObject? o)
    {
        HasSelection = o != null;
        if (o == null)
        {
            Title = "Nothing selected";
            KindText = "Click an object, or search for one by name.";
            Rows = Array.Empty<InfoRowViewModel>();
            return;
        }
        Title = o.Name;
        KindText = o switch
        {
            Galaxy => "Galaxy",
            GalaxyCluster => "Galaxy cluster",
            Quasar => "Quasar or active galaxy",
            _ => "",
        };
        Rows = ObjectInfoFormatter.Format(o)
            .Select(r => new InfoRowViewModel(r.Label, NoBreakInUnits(r.Value),
                                              r.GlossaryKey is { } k ? Glossary.Get(k) : null))
            .ToList();
    }

    /// <summary>Keeps "km/s" on one line (WPF may break a line after the slash).</summary>
    private static string NoBreakInUnits(string value) => value.Replace("km/s", "km\u2060/\u2060s");
}
