namespace Universe3D.Tests;

/// <summary>Timing tests run alone, after the parallel tests, so CPU contention cannot skew them.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerfCollection
{
    public const string Name = "Perf";
}
