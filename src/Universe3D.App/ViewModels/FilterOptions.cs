using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Universe3D.App.Resources;
using Universe3D.Core.Coordinates;
using Universe3D.Core.Display;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;
using Universe3D.Rendering;

namespace Universe3D.App.ViewModels;

/// <summary>A choice in the quasar-limit list.</summary>
public sealed record LimitOption(string Label, int Count);

/// <summary>
/// The left panel's filters and display options: maximum distance, type checkboxes, color mode, frame, layer
/// toggles, velocity arrows and the quasar limit. <see cref="Configure"/> resets the per-data-set parts.
/// </summary>
public sealed partial class FilterOptions : ObservableObject
{
    public const int DefaultQuasarLimit = 250_000;
    private const double MinDistanceMpc = 1e-3;

    public static string PeculiarUnavailableText { get; } =
        "Not available for this data set: a peculiar velocity needs a distance measured without using the redshift, " +
        "and galaxy clusters and quasars here have only redshift distances." + Environment.NewLine + Environment.NewLine +
        Glossary.Get("PeculiarVelocity");

    public ObservableCollection<TypeFilterItem> Types { get; } = new();

    public IReadOnlyList<LimitOption> QuasarLimitOptions { get; } = new LimitOption[]
    {
        new("25,000", 25_000), new("50,000", 50_000), new("100,000", 100_000), new("250,000", 250_000),
        new("All loaded", int.MaxValue),
    };

    [ObservableProperty] private DataSetKind? _kind;

    // Maximum distance, as log10(Mpc) on a logarithmic slider.
    [ObservableProperty] private double _distanceLogMin = -3;
    [ObservableProperty] private double _distanceLogMax = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxDistanceMpc), nameof(MaxDistanceText))]
    private double _distanceLog = 1;

    [ObservableProperty] private ColorMode _colorMode = ColorMode.Type;
    [ObservableProperty] private DisplayFrame _frame = DisplayFrame.Supergalactic;

    [ObservableProperty] private bool _showShapes = true;
    [ObservableProperty] private bool _showNames = true;
    [ObservableProperty] private bool _showRings = true;
    [ObservableProperty] private bool _showMilkyWay = true;

    [ObservableProperty] private bool _showArrows;
    [ObservableProperty] private ArrowMode _arrowMode = ArrowMode.Observed;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArrowScaleText))]
    private double _arrowScale = VelocityArrow.DefaultK;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeculiarToolTip))]
    private bool _peculiarAvailable = true;

    [ObservableProperty] private int _quasarLimit = DefaultQuasarLimit;
    [ObservableProperty] private bool _isQuasarLimitVisible;

    /// <summary>Objects farther than this are hidden; +∞ with the slider at its top.</summary>
    public double MaxDistanceMpc => DistanceLog >= DistanceLogMax - 1e-9 ? double.PositiveInfinity : Math.Pow(10, DistanceLog);

    public string MaxDistanceText => double.IsPositiveInfinity(MaxDistanceMpc)
        ? $"All, to {Units.FormatDistance(Math.Pow(10, DistanceLogMax))}"
        : Units.FormatDistance(MaxDistanceMpc);

    public string ArrowScaleText => ArrowScale.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    public string PeculiarToolTip => PeculiarAvailable ? Glossary.Get("PeculiarVelocity") : PeculiarUnavailableText;

    /// <summary>Bit <c>TypeCode</c> set = shown.</summary>
    public uint TypeMask
    {
        get
        {
            uint mask = uint.MaxValue;
            foreach (var t in Types)
                if (!t.IsChecked) mask &= ~(1u << (int)t.Bit);
            return mask;
        }
    }

    /// <summary>True when an object passes the distance and type filters (the same test the shaders apply).</summary>
    public static bool Passes(CosmicObject o, double maxDistanceMpc, uint typeMask) =>
        o.DistanceMpc <= maxDistanceMpc && ((typeMask >> (int)Math.Min(o.TypeCode, 31u)) & 1u) != 0;

    /// <summary>
    /// Sets up the filters for a newly shown data set: its types, its distance range (all shown), and velocity arrows
    /// off (Ruling R20). Peculiar velocity is only offered where distances are measured (data sets 1 and 2).
    /// </summary>
    public void Configure(DataSet ds)
    {
        Kind = ds.Kind;
        foreach (var t in Types) t.PropertyChanged -= OnTypeChanged;
        Types.Clear();
        foreach (var item in TypeItems(ds))
        {
            item.PropertyChanged += OnTypeChanged;
            Types.Add(item);
        }
        OnPropertyChanged(nameof(TypeMask));

        double lo = Math.Log10(Math.Max(ds.DistanceRangeMpc.Min, MinDistanceMpc));
        double hi = Math.Log10(Math.Max(ds.DistanceRangeMpc.Max, MinDistanceMpc));
        if (hi <= lo) hi = lo + 1;
        DistanceLogMin = lo;
        DistanceLogMax = hi;
        DistanceLog = hi;

        ShowArrows = false;
        PeculiarAvailable = ds.Kind is DataSetKind.LocalVolume or DataSetKind.NearbyUniverse;
        if (!PeculiarAvailable) ArrowMode = ArrowMode.Observed;
        IsQuasarLimitVisible = ds.Kind == DataSetKind.Quasars;
    }

    private void OnTypeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TypeFilterItem.IsChecked)) OnPropertyChanged(nameof(TypeMask));
    }

    private static IEnumerable<TypeFilterItem> TypeItems(DataSet ds)
    {
        var counts = ds.Objects.GroupBy(o => o.TypeCode).ToDictionary(g => g.Key, g => g.Count());
        IEnumerable<(uint Bit, string Label, string Keys)> defs = ds.Kind switch
        {
            DataSetKind.Clusters => new (uint, string, string)[]
            {
                (1, "X-ray clusters (MCXC only)", "Mcxc"),
                (2, "Optical clusters (Abell only)", "Abell"),
                (3, "In both MCXC and Abell", "Mcxc Abell"),
            },
            DataSetKind.Quasars => new (uint, string, string)[]
            {
                ((uint)QuasarType.Quasar, "Quasars", "Quasar"),
                ((uint)QuasarType.Agn, "AGN", "Agn"),
                ((uint)QuasarType.BlLac, "BL Lac objects", "BlLac"),
                ((uint)QuasarType.Type2, "Type 2 AGN", "Type2Agn"),
            },
            _ => new (uint, string, string)[]
            {
                ((uint)MorphologyClass.Elliptical, "Elliptical", "Elliptical"),
                ((uint)MorphologyClass.Lenticular, "Lenticular (S0)", "Lenticular"),
                ((uint)MorphologyClass.Spiral, "Spiral", "Spiral"),
                ((uint)MorphologyClass.BarredSpiral, "Barred spiral", "BarredSpiral"),
                ((uint)MorphologyClass.Irregular, "Irregular", "Irregular"),
                ((uint)MorphologyClass.DwarfSpheroidal, "Dwarf spheroidal", "DwarfSpheroidal"),
                ((uint)MorphologyClass.DwarfIrregular, "Dwarf irregular", "DwarfIrregular"),
                ((uint)MorphologyClass.Peculiar, "Peculiar or merging", "PeculiarGalaxy"),
                ((uint)MorphologyClass.Unknown, ObjectInfoFormatter.Unclassified, "Unclassified"),
            },
        };
        foreach (var (bit, label, keys) in defs)
        {
            if (!counts.TryGetValue(bit, out int n) || n == 0) continue;
            var c = PointCloudRenderer.TypeColor(ds.Kind, bit);
            yield return new TypeFilterItem(bit, label, n, GlossaryExtension.Text(keys),
                Color.FromRgb((byte)(255 * c.X), (byte)(255 * c.Y), (byte)(255 * c.Z)));
        }
    }
}
