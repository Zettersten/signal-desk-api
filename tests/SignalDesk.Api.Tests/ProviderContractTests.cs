using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;

namespace SignalDesk.Api.Tests;

public sealed class ProviderContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Perplexity_uses_search_endpoint_filters_and_preserves_unknown_dates()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<SignalDesk.Api.Data.DeskDb>>();
        var ledger = new AiLedger(dbFactory, Options.Create(new DeskOptions
        { Mode = "live", EnableLiveAi = true, DailyBudgetUsd = 1 }), TimeProvider.System);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["PERPLEXITY_API_KEY"] = "fixture-not-a-real-key" }).Build();
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://api.perplexity.ai/search", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(Ct));
            Assert.Equal(20, json.RootElement.GetProperty("max_results").GetInt32());
            Assert.Equal("iceye.com", json.RootElement.GetProperty("search_domain_filter")[0].GetString());
            Assert.False(json.RootElement.TryGetProperty("model", out _));
            return Response("""{"id":"fixture-search","results":[{"title":"Announcement","url":"https://iceye.com/news/one","snippet":"Permitted excerpt."}]}""");
        }));
        var service = new PerplexitySearchService(http, configuration, ledger,
            Options.Create(new AiOptions { SearchQueryReserveUsd = .01m }));
        var result = await service.SearchAsync(["ICEYE financing"], ["iceye.com"], DateTime.UtcNow.AddDays(-2), Ct);
        Assert.Single(result.Results);
        Assert.Null(result.Results[0].Date);
        await using var db = await dbFactory.CreateDbContextAsync(Ct);
        Assert.Equal("fixture-search", (await db.AiExecutions.SingleAsync(Ct)).RequestId);
    }

    [Fact]
    public async Task Missing_typesafe_answers_are_not_cached_as_complete_and_do_not_recharge()
    {
        using var factory = new DeskFactory("live");
        _ = factory.CreateClient();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<SignalDesk.Api.Data.DeskDb>>();
        var ledger = new AiLedger(dbFactory, Options.Create(new DeskOptions
        { Mode = "live", EnableLiveAi = true, DailyBudgetUsd = 1 }), TimeProvider.System);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["TYPESAFE_API_KEY"] = "fixture-not-a-real-key" }).Build();
        var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal("https://api.typesafe.ai/v1/systemone", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(Response("""{"model":"jev-1.14.0","answers":{}}"""));
        }));
        var service = new TypeSafeDecisionService(http, configuration, ledger, Options.Create(new AiOptions { DecisionReserveUsd = .01m }));
        await Assert.ThrowsAsync<ApiException>(() => service.SupportsAsync(new { source = "Evidence." }, new { claim = "A claim." }, Ct));
        var retry = await Assert.ThrowsAsync<ApiException>(() => service.SupportsAsync(new { source = "Evidence." }, new { claim = "A claim." }, Ct));
        Assert.Equal(409, retry.Status);
        Assert.Equal(1, calls);
        await using var db = await dbFactory.CreateDbContextAsync(Ct);
        var row = await db.AiExecutions.SingleAsync(Ct);
        Assert.Null(row.ResultJson);
        Assert.Equal("failed-or-unknown", row.Status);
    }

    private static HttpResponseMessage Response(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
}
