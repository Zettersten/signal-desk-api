using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Tests;

public sealed class DeskFactory(string mode = "sample", string environment = "Testing",
    bool collectors = false, Action<IServiceCollection>? customize = null) : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"signal-desk-test-{Guid.NewGuid():N}");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Desk:DatabasePath", Path.Combine(directory, "test.db"));
        builder.UseSetting("Desk:Mode", mode);
        builder.UseSetting("Desk:EnableLiveAi", collectors ? "true" : "false");
        builder.UseSetting("Desk:EnableCollectors", collectors ? "true" : "false");
        if (customize is not null) builder.ConfigureServices(customize);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (disposing && Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

public sealed class ApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Catalog_preserves_frontend_links_and_required_profile_fields()
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/catalog", Ct);
        response.EnsureSuccessStatusCode();
        var catalog = (await response.Content.ReadFromJsonAsync<Catalog>(Wire.Json, Ct))!;
        Assert.Equal("sample", catalog.Mode);
        Assert.Equal(14, catalog.Records.Length);
        Assert.Equal(12, catalog.Competitors.Length);
        Assert.Equal(7, catalog.Publishers.Length);
        Assert.Equal(14, catalog.Records.Select(x => x.Id).Distinct().Count());
        foreach (var record in catalog.Records)
        {
            Assert.True(record.Sample);
            Assert.Equal(record.Company, catalog.Competitors.Single(c => c.Id == record.CompetitorId).Name);
            Assert.Equal(record.Publisher, catalog.Publishers.Single(p => p.Id == record.PublisherId).Name);
            Assert.Contains(record.Category, Wire.Categories);
            Assert.InRange(record.Importance, 0, 100);
            Assert.NotEmpty(record.Paragraphs);
        }
        foreach (var competitor in catalog.Competitors)
        {
            Assert.False(string.IsNullOrEmpty(competitor.Image.Src));
            Assert.Equal(4, competitor.Financials.Length);
        }
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog");
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(conditional, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", Ct)).StatusCode);
    }

    [Fact]
    public async Task Single_record_brief_uses_authoritative_evidence_not_client_text()
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        var catalog = (await client.GetFromJsonAsync<Catalog>("/v1/catalog", Wire.Json, Ct))!;
        var record = catalog.Records[0];
        var request = new BriefInput([record with { Title = "CLIENT INJECTION" }], "Compare market position",
            "Strategy leadership", "Summarize");
        var response = await client.PostAsJsonAsync("/v1/briefs", request, Wire.Json, Ct);
        response.EnsureSuccessStatusCode();
        var brief = (await response.Content.ReadFromJsonAsync<Brief>(Wire.Json, Ct))!;
        Assert.Contains("Sample analysis", brief.Text);
        Assert.Contains(record.Title, brief.Text);
        Assert.DoesNotContain("CLIENT INJECTION", brief.Text);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"records\":[],\"objective\":\"x\",\"audience\":\"Strategy leadership\"}")]
    public async Task Invalid_briefs_are_client_errors(string body)
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/v1/briefs", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scalar_and_OpenApi_expose_real_contracts_without_provider_calls()
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        Assert.Contains("Signal Desk API", await client.GetStringAsync("/scalar/v1", Ct));
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct);
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/v1/catalog", out _));
        Assert.True(paths.TryGetProperty("/v1/briefs", out _));
        Assert.True(paths.TryGetProperty("/v1/ai/services", out _));
        var services = await client.GetStringAsync("/v1/ai/services", Ct);
        Assert.Contains("typesafe-decisions", services);
        Assert.Contains("perplexity-search", services);
        Assert.DoesNotContain("API_KEY", services);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/v1/ingestion/runs", new { sourceId = "spacenews" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Live_seed_imports_all_sources_without_sample_financials()
    {
        using var factory = new DeskFactory("live");
        using var client = factory.CreateClient();
        var catalog = (await client.GetFromJsonAsync<Catalog>("/v1/catalog", Wire.Json, Ct))!;
        Assert.Equal("live", catalog.Mode);
        Assert.Empty(catalog.Records);
        Assert.Equal(98, catalog.Publishers.Length);
        Assert.All(catalog.Competitors, c =>
        {
            Assert.Null(c.Incorporated);
            Assert.All(c.Financials, f => Assert.Null(f.Value));
        });
        var sources = await client.GetFromJsonAsync<JsonElement[]>("/v1/sources", Ct);
        Assert.Equal(98, sources!.Length);
        Assert.All(sources, s => Assert.False(s.GetProperty("approved").GetBoolean()));
    }

    [Fact]
    public async Task Production_never_falls_back_to_local_sqlite()
    {
        using var factory = new DeskFactory(environment: "Production");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/v1/catalog", Ct)).StatusCode);
    }

    [Fact]
    public async Task Resource_routes_pagination_and_cors_are_bounded()
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        var page = (await client.GetFromJsonAsync<SignalPageProxy>("/v1/signals?limit=2", Wire.Json, Ct))!;
        Assert.Equal(2, page.Records.Length);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/signals/missing", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/v1/signals?limit=1000", Ct)).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/briefs");
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var cors = await client.SendAsync(request, Ct);
        Assert.False(cors.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public sealed record SignalPageProxy(Signal[] Records, string? NextCursor);

    [Theory]
    [InlineData("rebound.attacker.example", null)]
    [InlineData("localhost", "https://untrusted.example")]
    public async Task Local_authentication_rejects_untrusted_host_or_origin(string host, string? origin)
    {
        using var factory = new DeskFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ai/services");
        request.Headers.Host = host;
        if (origin is not null) request.Headers.Add("Origin", origin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request, Ct)).StatusCode);
    }
}
