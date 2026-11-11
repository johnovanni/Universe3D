using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Universe3D.Core.Catalogs;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Display;
using Universe3D.Core.Objects;
using Universe3D.Core.Text;
using Universe3D.Rendering;

namespace Universe3D.App.ViewModels;

/// <summary>A search hit: the object and a short description such as "Galaxy · 53.8 Mly".</summary>
public sealed record SearchResult(CosmicObject Object, string Name, string Detail);

/// <summary>
/// The window's state: the data sets and the one on screen, filters, search, the selected object and its information,
/// the catalog area and the status bar. The scene controller draws what this describes and reports camera and frame
/// rate back.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDataSetStore _store;
    private readonly DownloadService _downloads;
    private CancellationTokenSource? _downloadCts;
    private DataSet? _loaded;
    private Task<NameSearch>? _searchIndex;
    private int _loadGeneration, _searchGeneration;
    private bool _preloadStarted;

    public MainViewModel(IDataSetStore store, DownloadService downloads)
    {
        _store = store;
        _downloads = downloads;
        DataSets = Enum.GetValues<DataSetKind>().Select(k => new DataSetViewModel(k)).ToList();
        Filters.PropertyChanged += OnFiltersChanged;
    }

    public CosmologyModel Cosmology { get; } = CosmologyModel.Default;
    public IReadOnlyList<DataSetViewModel> DataSets { get; }
    public FilterOptions Filters { get; } = new();
    public InfoViewModel Info { get; } = new();

    /// <summary>Load the other data sets in the background after the first, to show their counts (off for --bench).</summary>
    public bool PreloadOtherDataSets { get; set; } = true;

    [ObservableProperty] private DataSetViewModel? _selectedDataSet;

    /// <summary>The data set on screen: the loaded one, with quasars cut to the quasar limit (brightest first).</summary>
    [ObservableProperty] private DataSet? _shown;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CenterOnSelectedCommand), nameof(FlyToSelectedCommand))]
    private CosmicObject? _selected;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<SearchResult> _searchResults = Array.Empty<SearchResult>();
    [ObservableProperty] private SearchResult? _selectedSearchResult;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _dataSetStatus = "Starting…";
    [ObservableProperty] private string _objectsStatus = "";
    [ObservableProperty] private string _excludedStatus = "";
    [ObservableProperty] private string _cameraStatus = "";
    [ObservableProperty] private string _frameRateStatus = "";
    [ObservableProperty] private string _warning = "";
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty] private string _catalogSources = "";

    /// <summary>Progress, result or failure of "Download latest"; also shown in the status bar.</summary>
    [ObservableProperty] private string _downloadStatus = "";

    /// <summary>True while a download runs (one at a time).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadLatestCommand), nameof(CancelDownloadCommand),
                                nameof(UseBuiltInCommand))]
    private bool _isDownloading;

    /// <summary>"reduced defaults for this GPU", shown once in the status bar.</summary>
    [ObservableProperty] private string _gpuNote = "";

    /// <summary>"running on a USB display adapter" while the window is on one.</summary>
    [ObservableProperty] private string _adapterNote = "";

    /// <summary>Fly the camera to an object (double-click, search, "Fly to").</summary>
    public event Action<CosmicObject>? FlyToRequested;

    /// <summary>Home: fit the data set.</summary>
    public event Action? HomeRequested;

    /// <summary>F: center on the selection, keeping the zoom.</summary>
    public event Action? CenterRequested;

    public void Select(CosmicObject? o, bool flyTo)
    {
        Selected = o;
        if (flyTo && o != null) FlyToRequested?.Invoke(o);
    }

    [RelayCommand]
    private void Home() => HomeRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CenterOnSelected() => CenterRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FlyToSelected()
    {
        if (Selected != null) FlyToRequested?.Invoke(Selected);
    }

    private bool HasSelection() => Selected != null;

    /// <summary>Enter in the search box: go to the best match.</summary>
    [RelayCommand]
    private async Task ChooseFirstResultAsync()
    {
        await SearchAsync(SearchText);
        if (SearchResults.Count > 0) Select(SearchResults[0].Object, flyTo: true);
    }

    [RelayCommand(CanExecute = nameof(CanUseBuiltIn))]
    private async Task UseBuiltInAsync()
    {
        if (SelectedDataSet is not { } dsvm) return;
        _store.ClearDownloaded(dsvm.Kind);
        DownloadStatus = "";
        dsvm.Forget();
        await ActivateAsync(dsvm);
    }

    private bool CanUseBuiltIn() => SelectedDataSet is { FromCache: true } && !IsDownloading;

    /// <summary>
    /// Rebuilds the selected data set from VizieR in the background. The view is replaced only when the build
    /// completes and that data set is the one on screen; otherwise the new copy loads when it is next selected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadLatestAsync()
    {
        if (SelectedDataSet is not { } dsvm) return;
        var kind = dsvm.Kind;
        string title = dsvm.Title;
        using var cts = new CancellationTokenSource();
        _downloadCts = cts;
        IsDownloading = true;
        DownloadStatus = $"Downloading {title}…";
        var progress = new Progress<string>(text => DownloadStatus = $"{title}: {text}");
        try
        {
            var ds = await Task.Run(() => _downloads.DownloadAsync(kind, progress, cts.Token));
            DownloadStatus = $"Downloaded {title}: {ds.Objects.Count:N0} objects.";
            var entry = DataSets.First(d => d.Kind == kind);
            entry.Forget();
            if (SelectedDataSet?.Kind == kind) await ActivateAsync(entry);
            else entry.Summary = "Downloaded; loads when selected";
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = $"Download of {title} cancelled. Nothing was changed.";
        }
        catch (VizierUnavailableException ex)
        {
            DownloadStatus = $"Could not download {title}; the existing data was kept. {ex.Message}";
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Download of {title} failed; the existing data was kept. {ex.Message}";
        }
        finally
        {
            _downloadCts = null;
            IsDownloading = false;
        }
    }

    private bool CanDownload() => !IsDownloading;

    /// <summary>
    /// Applies the defaults for the GPU class found at startup (spec §1). Settings the user already changed are kept;
    /// the reduced-defaults note is shown once, then cleared.
    /// </summary>
    public async void ApplyGpuDefaults(GpuDefaults defaults)
    {
        if (Filters.QuasarLimit == FilterOptions.DefaultQuasarLimit) Filters.QuasarLimit = defaults.QuasarLimit;
        if (Filters.ShapeCap == ShapeRenderer.RecommendedCap) Filters.ShapeCap = defaults.ShapeCap;
        if (!defaults.Reduced) return;
        GpuNote = "reduced defaults for this GPU";
        await Task.Delay(TimeSpan.FromSeconds(30));
        GpuNote = "";
    }

    [RelayCommand(CanExecute = nameof(IsDownloading))]
    private void CancelDownload() => _downloadCts?.Cancel();

    partial void OnSelectedDataSetChanged(DataSetViewModel? value)
    {
        if (!IsDownloading) DownloadStatus = "";
        if (value != null) _ = ActivateAsync(value);
    }

    partial void OnSelectedChanged(CosmicObject? value) => Info.Show(value, Cosmology);

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(value);

    partial void OnSelectedSearchResultChanged(SearchResult? value)
    {
        if (value != null) Select(value.Object, flyTo: true);
    }

    private async Task ActivateAsync(DataSetViewModel dsvm)
    {
        int generation = ++_loadGeneration;
        IsLoading = true;
        DataSetStatus = $"Loading {dsvm.Title}…";
        try
        {
            var ds = await dsvm.LoadAsync(_store);
            if (generation != _loadGeneration) return;
            _loaded = ds;
            Selected = null;
            SearchText = "";
            Filters.Configure(ds);
            ApplyLimit();
            DataSetStatus = dsvm.Title;
            CatalogStatus = (dsvm.FromCache ? "Downloaded copy" : "Built-in snapshot") +
                            $", built {ds.BuiltUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            CatalogSources = ds.SourceSummary +
                             (ds.SkippedRows > 0 ? $". {ds.SkippedRows:N0} catalog rows skipped (incomplete)." : "");
            UseBuiltInCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            // A newer selection owns the status; this failure shows in the entry's summary.
            if (generation == _loadGeneration) DataSetStatus = $"Could not load {dsvm.Title}: {ex.Message}";
        }
        finally
        {
            if (generation == _loadGeneration) IsLoading = false;
        }
        await PreloadOthersAsync();
    }

    /// <summary>Loads the remaining data sets once, so the list box shows every count and range.</summary>
    private async Task PreloadOthersAsync()
    {
        if (_preloadStarted || !PreloadOtherDataSets) return;
        _preloadStarted = true;
        foreach (var other in DataSets.Where(d => d.Data == null))
        {
            try { await other.LoadAsync(_store); }
            catch (Exception) { /* the entry says it could not be loaded; selecting it retries and reports why */ }
        }
    }

    private void OnFiltersChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FilterOptions.QuasarLimit):
                ApplyLimit();
                break;
            case nameof(FilterOptions.TypeMask):
            case nameof(FilterOptions.MaxDistanceMpc):
                UpdateObjectsStatus();
                break;
        }
    }

    private void ApplyLimit()
    {
        if (_loaded is not { } ds) return;
        int limit = Filters.QuasarLimit;
        var shown = ds.Kind == DataSetKind.Quasars && limit < ds.Objects.Count
            ? new DataSet(ds.Kind, ds.Objects.Take(limit).ToList(), ds.ExcludedCount, ds.SkippedRows, ds.BuiltUtc,
                          ds.SourceSummary)
            : ds;
        if (Selected != null && !shown.Objects.Contains(Selected)) Selected = null;
        _searchIndex = Task.Run(() => new NameSearch(shown.Objects));
        Shown = shown;
        UpdateObjectsStatus();
        if (SearchText.Length > 0) _ = SearchAsync(SearchText);
    }

    private void UpdateObjectsStatus()
    {
        if (Shown is not { } ds) return;
        double max = Filters.MaxDistanceMpc;
        uint mask = Filters.TypeMask;
        int visible = ds.Objects.Count(o => FilterOptions.Passes(o, max, mask));
        ObjectsStatus = string.Create(CultureInfo.InvariantCulture,
            $"{visible:N0} of {ds.Objects.Count:N0} objects shown");
        ExcludedStatus = ds.ExcludedCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{ds.ExcludedCount:N0} left out (no valid distance)")
            : "";
    }

    private async Task SearchAsync(string text)
    {
        int generation = ++_searchGeneration;
        if (string.IsNullOrWhiteSpace(text) || _searchIndex is not { } indexTask)
        {
            SearchResults = Array.Empty<SearchResult>();
            return;
        }
        var index = await indexTask;
        var found = await Task.Run(() => index.Find(text));
        if (generation != _searchGeneration) return;
        SearchResults = found.Select(o => new SearchResult(o, o.Name, Describe(o))).ToList();
    }

    private static string Describe(CosmicObject o)
    {
        string kind = o switch
        {
            Galaxy => "Galaxy",
            GalaxyCluster => "Cluster",
            Quasar q => q.Type switch
            {
                QuasarType.Quasar => "Quasar",
                QuasarType.BlLac => "BL Lac",
                _ => "AGN",
            },
            _ => "",
        };
        return $"{kind} · {Units.FormatLightYears(o.DistanceMpc)}";
    }
}
