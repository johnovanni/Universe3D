using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>The only way data sets are read or written; no other code touches data files.</summary>
public interface IDataSetStore
{
    DataSet Load(DataSetKind kind, out bool fromCache);
    void Save(DataSet dataSet);
    bool HasDownloadedCopy(DataSetKind kind);
    void ClearDownloaded(DataSetKind kind);
}
