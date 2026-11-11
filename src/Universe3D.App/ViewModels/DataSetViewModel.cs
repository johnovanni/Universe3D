using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Universe3D.App.Resources;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;

namespace Universe3D.App.ViewModels;

/// <summary>One entry of the data-set list box: its title, and once loaded its object count and distance range.</summary>
public sealed partial class DataSetViewModel : ObservableObject
{
    private Task<DataSet>? _loading;

    public DataSetViewModel(DataSetKind kind)
    {
        Kind = kind;
        string glossaryKeys;
        (Number, Title, Description, glossaryKeys) = kind switch
        {
            DataSetKind.LocalVolume => (1, "Local Group & Local Volume",
                "About 870 galaxies within about 85 million light-years, with measured distances (UNGC, McConnachie).",
                "LocalGroup MeasuredDistance"),
            DataSetKind.NearbyUniverse => (2, "Nearby universe",
                "The 2MASS Redshift Survey: about 43,500 galaxies over the whole sky, out to about 2 billion light-years.",
                "Redshift"),
            DataSetKind.Clusters => (3, "Galaxy clusters",
                "Galaxy clusters found by their X-rays (MCXC) or by counting galaxies (Abell), with measured redshifts.",
                "Mcxc Abell"),
            DataSetKind.Quasars => (4, "Quasars & AGN",
                "Quasars and other active galaxies from the Million Quasars catalog, out to the early universe.",
                "Quasar Agn"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        ToolTip = Description + Environment.NewLine + Environment.NewLine + GlossaryExtension.Text(glossaryKeys);
    }

    public DataSetKind Kind { get; }
    public int Number { get; }
    public string Title { get; }
    public string Description { get; }

    /// <summary>The description plus the Glossary texts of the terms in the entry (spec §5).</summary>
    public string ToolTip { get; }

    [ObservableProperty]
    private string _summary = "Not loaded yet";

    public DataSet? Data { get; private set; }

    /// <summary>True when the data came from a downloaded copy rather than the built-in snapshot.</summary>
    public bool FromCache { get; private set; }

    /// <summary>Why a downloaded copy was not used (it was unreadable and has been deleted), or null.</summary>
    public string? CacheProblem { get; private set; }

    /// <summary>Loads once (off the UI thread); later calls return the same task. A failed load can be retried.</summary>
    public async Task<DataSet> LoadAsync(IDataSetStore store)
    {
        _loading ??= Task.Run(() =>
        {
            var loaded = store.Load(Kind, out bool fromCache, out string? cacheProblem);
            FromCache = fromCache;
            CacheProblem = cacheProblem;
            return loaded;
        });
        try
        {
            var ds = await _loading;
            Data = ds;
            Summary = SummaryOf(ds);
            return ds;
        }
        catch
        {
            _loading = null;
            Summary = "Could not be loaded";
            throw;
        }
    }

    /// <summary>Drops the loaded data so the next <see cref="LoadAsync"/> reads the store again.</summary>
    public void Forget()
    {
        _loading = null;
        Data = null;
        FromCache = false;
        CacheProblem = null;
        Summary = "Not loaded yet";
    }

    /// <summary>"871 objects · 23 kly to 85.4 Mly".</summary>
    public static string SummaryOf(DataSet ds) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{ds.Objects.Count:N0} objects · {Units.FormatLightYears(ds.DistanceRangeMpc.Min)} to {Units.FormatLightYears(ds.DistanceRangeMpc.Max)}");
}
