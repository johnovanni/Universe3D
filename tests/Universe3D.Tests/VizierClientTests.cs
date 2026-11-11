using System.Net;
using Universe3D.Core.Catalogs;

namespace Universe3D.Tests;

public class VizierClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<string> Urls { get; } = new();
        public List<string> UserAgents { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.OriginalString);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(_respond(request));
        }
    }

    private static readonly string[] Mirrors = { "https://host1.test/asu-tsv", "https://host2.test/asu-tsv" };

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static readonly string[] Cols = { "RAJ2000", "DEJ2000", "z" };

    [Fact]
    public async Task Client_FallsBackToSecondMirror()
    {
        var h = new FakeHandler(r => r.RequestUri!.Host == "host1.test"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Ok(TsvTableTests.Sample));
        var c = new VizierClient(new HttpClient(h), Mirrors);
        var t = await c.QueryAsync("VII/294/catalog", Cols, null, CancellationToken.None);
        Assert.Equal(3, t.Count);
        Assert.Equal(2, h.Urls.Count);
        Assert.StartsWith(Mirrors[0], h.Urls[0]);
        Assert.StartsWith(Mirrors[1], h.Urls[1]);
        Assert.Equal("Universe3D/1.0", h.UserAgents[0]);
    }

    [Fact]
    public async Task Client_AllMirrorsDown_Throws()
    {
        var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var c = new VizierClient(new HttpClient(h), Mirrors);
        var ex = await Assert.ThrowsAsync<VizierUnavailableException>(
            () => c.QueryAsync("x", Cols, null, CancellationToken.None));
        Assert.Contains("host1.test", ex.Message);
        Assert.Contains("host2.test", ex.Message);
    }

    [Fact]
    public async Task Client_HtmlErrorPageOrEmptyBody_CountsAsFailure()
    {
        var h = new FakeHandler(r => r.RequestUri!.Host == "host1.test"
            ? Ok("  <html><body>Page unavailable</body></html>")
            : Ok("# only comments\n\n"));
        var c = new VizierClient(new HttpClient(h), Mirrors);
        await Assert.ThrowsAsync<VizierUnavailableException>(
            () => c.QueryAsync("x", Cols, null, CancellationToken.None));
        Assert.Equal(2, h.Urls.Count);
    }

    [Fact]
    public async Task Client_NetworkException_FallsBack()
    {
        int n = 0;
        var h = new FakeHandler(_ => n++ == 0 ? throw new HttpRequestException("boom") : Ok(TsvTableTests.Sample));
        var c = new VizierClient(new HttpClient(h), Mirrors);
        var t = await c.QueryAsync("x", Cols, null, CancellationToken.None);
        Assert.Equal(3, t.Count);
    }

    [Fact]
    public async Task Client_BuildsQuery()
    {
        var h = new FakeHandler(_ => Ok(TsvTableTests.Sample));
        var c = new VizierClient(new HttpClient(h), Mirrors);
        await c.QueryAsync("VII/294/catalog", Cols, new Dictionary<string, string> { ["z"] = ">0" }, CancellationToken.None);
        var url = h.Urls[0];
        Assert.Contains("-source=VII%2F294%2Fcatalog", url);
        Assert.Contains("-out=RAJ2000,DEJ2000,z", url);
        Assert.Contains("-out.max=unlimited", url);
        Assert.Contains("z=%3E0", url);
    }
}
