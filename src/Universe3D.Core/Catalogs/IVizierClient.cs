namespace Universe3D.Core.Catalogs;

/// <summary>Queries a VizieR catalog and returns the result as a table.</summary>
public interface IVizierClient
{
    Task<TsvTable> QueryAsync(string source, IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string>? constraints, CancellationToken ct);
}

/// <summary>Thrown when every VizieR mirror failed. The message lists each mirror and its reason.</summary>
public sealed class VizierUnavailableException : Exception
{
    public VizierUnavailableException(string message) : base(message) { }
    public VizierUnavailableException(string message, Exception inner) : base(message, inner) { }
}
