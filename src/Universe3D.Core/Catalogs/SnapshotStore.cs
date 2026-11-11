using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Stores data sets as gzip snapshots: a downloaded copy in the cache folder, falling back to the
/// built-in snapshot (an embedded resource by default).
/// </summary>
public sealed class SnapshotStore : IDataSetStore
{
    private readonly string _cacheDir;
    private readonly Func<DataSetKind, Stream> _builtIn;

    public SnapshotStore(string? cacheDir = null, Func<DataSetKind, Stream>? builtIn = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Universe3D");
        _builtIn = builtIn ?? OpenEmbedded;
    }

    public static string FileName(DataSetKind kind) => kind switch
    {
        DataSetKind.LocalVolume => "localvolume.u3d.gz",
        DataSetKind.NearbyUniverse => "nearby.u3d.gz",
        DataSetKind.Clusters => "clusters.u3d.gz",
        DataSetKind.Quasars => "quasars.u3d.gz",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private string CachePath(DataSetKind kind) => Path.Combine(_cacheDir, FileName(kind));

    public DataSet Load(DataSetKind kind, out bool fromCache)
    {
        string path = CachePath(kind);
        if (File.Exists(path))
        {
            try
            {
                DataSet cached;
                using (var fs = File.OpenRead(path)) cached = SnapshotSerializer.Read(fs);
                if (cached.Kind != kind) throw new InvalidDataException("Snapshot is for a different data set.");
                fromCache = true;
                return cached;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException
                                          or OverflowException or NotSupportedException)
            {
                try { File.Delete(path); }
                catch (Exception d) when (d is IOException or UnauthorizedAccessException) { }
            }
        }
        fromCache = false;
        using var stream = _builtIn(kind);
        return SnapshotSerializer.Read(stream);
    }

    public void Save(DataSet dataSet)
    {
        Directory.CreateDirectory(_cacheDir);
        string path = CachePath(dataSet.Kind);
        string tmp = path + ".tmp";
        using (var fs = File.Create(tmp)) SnapshotSerializer.Write(fs, dataSet);
        File.Move(tmp, path, overwrite: true);
    }

    public bool HasDownloadedCopy(DataSetKind kind) => File.Exists(CachePath(kind));

    public void ClearDownloaded(DataSetKind kind)
    {
        string path = CachePath(kind);
        if (File.Exists(path)) File.Delete(path);
    }

    private static Stream OpenEmbedded(DataSetKind kind)
    {
        string file = FileName(kind);
        var asm = typeof(SnapshotStore).Assembly;
        string? res = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + file, StringComparison.OrdinalIgnoreCase));
        if (res == null)
            throw new FileNotFoundException($"Built-in snapshot '{file}' is not embedded in {asm.GetName().Name}.", file);
        return asm.GetManifestResourceStream(res)
            ?? throw new FileNotFoundException($"Built-in snapshot '{file}' could not be opened.", file);
    }
}
