using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>The only way data sets are read or written; no other code touches data files.</summary>
public interface IDataSetStore
{
    /// <summary>
    /// The downloaded copy if there is a readable one, else the built-in snapshot. When a downloaded copy existed but
    /// could not be used, <paramref name="cacheProblem"/> says why (and that it was deleted); otherwise it is null.
    /// </summary>
    DataSet Load(DataSetKind kind, out bool fromCache, out string? cacheProblem);
    void Save(DataSet dataSet);
    bool HasDownloadedCopy(DataSetKind kind);
    void ClearDownloaded(DataSetKind kind);
}
