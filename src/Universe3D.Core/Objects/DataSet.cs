namespace Universe3D.Core.Objects;

public sealed class DataSet
{
    public DataSet(DataSetKind kind, IReadOnlyList<CosmicObject> objects, int excludedCount,
                   int skippedRows, DateTime builtUtc, string sourceSummary)
    {
        Kind = kind;
        Objects = objects;
        ExcludedCount = excludedCount;
        SkippedRows = skippedRows;
        BuiltUtc = builtUtc;
        SourceSummary = sourceSummary;
        if (objects.Count == 0) DistanceRangeMpc = (0, 0);
        else
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var o in objects)
            {
                if (o.DistanceMpc < min) min = o.DistanceMpc;
                if (o.DistanceMpc > max) max = o.DistanceMpc;
            }
            DistanceRangeMpc = (min, max);
        }
    }

    public DataSetKind Kind { get; }
    public IReadOnlyList<CosmicObject> Objects { get; }
    public int ExcludedCount { get; }
    public int SkippedRows { get; }
    public DateTime BuiltUtc { get; }
    public string SourceSummary { get; }
    public (double Min, double Max) DistanceRangeMpc { get; }
}
