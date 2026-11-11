using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>Builds one data set from VizieR catalogs.</summary>
public interface IDataSetBuilder
{
    DataSetKind Kind { get; }

    Task<DataSet> BuildAsync(IVizierClient client, IProgress<string>? progress, CancellationToken ct);
}
