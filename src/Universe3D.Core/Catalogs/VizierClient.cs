using System.Net.Http.Headers;
using System.Text;

namespace Universe3D.Core.Catalogs;

/// <summary>VizieR ASU-TSV client that tries each mirror in turn.</summary>
public sealed class VizierClient : IVizierClient
{
    public static readonly IReadOnlyList<string> DefaultMirrors = new[]
    {
        "https://vizier.cfa.harvard.edu/viz-bin/asu-tsv",
        "https://vizier.cds.unistra.fr/viz-bin/asu-tsv",
    };

    private readonly HttpClient _http;
    private readonly IReadOnlyList<string> _mirrors;

    public VizierClient(HttpClient http, IReadOnlyList<string>? mirrors = null)
    {
        _http = http;
        _mirrors = mirrors ?? DefaultMirrors;
    }

    public async Task<TsvTable> QueryAsync(string source, IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string>? constraints, CancellationToken ct)
    {
        string query = BuildQuery(source, columns, constraints);
        var reasons = new List<string>();
        Exception? last = null;
        foreach (var mirror in _mirrors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, mirror + "?" + query);
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("Universe3D", "1.0"));
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    reasons.Add($"{mirror}: HTTP {(int)resp.StatusCode}");
                    continue;
                }
                string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (body.TrimStart().StartsWith('<'))
                {
                    reasons.Add($"{mirror}: HTML error page returned");
                    continue;
                }
                if (!TsvTable.HasHeader(body))
                {
                    reasons.Add($"{mirror}: response has no header row");
                    continue;
                }
                return TsvTable.Parse(new StringReader(body));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                last = ex;
                reasons.Add($"{mirror}: {ex.Message}");
            }
        }
        string msg = "All VizieR mirrors failed. " + string.Join("; ", reasons);
        throw last != null ? new VizierUnavailableException(msg, last) : new VizierUnavailableException(msg);
    }

    internal static string BuildQuery(string source, IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string>? constraints)
    {
        var sb = new StringBuilder();
        sb.Append("-source=").Append(Uri.EscapeDataString(source));
        sb.Append("&-out=").Append(string.Join(",", columns.Select(Uri.EscapeDataString)));
        sb.Append("&-out.max=unlimited");
        if (constraints != null)
            foreach (var kv in constraints)
                sb.Append('&').Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
        return sb.ToString();
    }
}
