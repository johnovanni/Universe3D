using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
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
        (Number, Title, Description) = kind switch
        {
            DataSetKind.LocalVolume => (1, "Local Group & Local Volume",
                "About 870 galaxies within about 85 million light-years, with measured distances (UNGC, McConnachie)."),
            DataSetKind.NearbyUniverse => (2, "Nearby universe",
                "The 2MASS Redshift Survey: about 43,500 galaxies over the whole sky, out to about 2 billion light-years."),
            DataSetKind.Clusters => (3, "Galaxy clusters",
                "Galaxy clusters found by their X-rays (MCXC) or by counting galaxies (Abell), with measured redshifts."),
            DataSetKind.Quasars => (4, "Quasars & AGN",
                "Quasars and other active galaxies from the Million Quasars catalog, out to the early universe."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public DataSetKind Kind { get; }
    public int Number { get; }
    public string Title { get; }
    public string Description { get; }

    [ObservableProperty]
    private string _summary = "Not loaded yet";

    public DataSet? Data { get; private set; }

    /// <summary>True when the data came from a downloaded copy rather than the built-in snapshot.</summary>
    public bool FromCache { get; private set; }

    /// <summary>Loads once (off the UI thread); later calls return the same task. A failed load can be retried.</summary>
    public async Task<DataSet> LoadAsync(IDataSetStore store)
    {
        _loading ??= Task.Run(() =>
        {
            var loaded = store.Load(Kind, out bool fromCache);
            FromCache = fromCache;
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
        Summary = "Not loaded yet";
    }

    /// <summary>"871 objects · 23 kly to 85.4 Mly".</summary>
    public static string SummaryOf(DataSet ds) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{ds.Objects.Count:N0} objects · {Units.FormatLightYears(ds.DistanceRangeMpc.Min)} to {Units.FormatLightYears(ds.DistanceRangeMpc.Max)}");
}
