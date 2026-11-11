using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Universe3D.App.ViewModels;

/// <summary>One type checkbox: a galaxy class, cluster source or quasar type, by its shader bit (TypeCode).</summary>
public sealed partial class TypeFilterItem : ObservableObject
{
    public TypeFilterItem(uint bit, string label, int count, string toolTip, Color color)
    {
        Bit = bit;
        Label = label;
        Count = count;
        ToolTip = toolTip;
        Swatch = new SolidColorBrush(color);
        Swatch.Freeze();
    }

    public uint Bit { get; }
    public string Label { get; }
    public int Count { get; }
    public string ToolTip { get; }
    public SolidColorBrush Swatch { get; }

    public string CountText => Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool _isChecked = true;
}
