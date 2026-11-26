using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Scalar.AspNetCore;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Data;
using SignalDesk.Api.AI;
using SignalDesk.Api.Ingestion;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<DeskOptions>(builder.Configuration.GetSection("Desk"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AiServices"));
var desk = builder.Configuration.GetSection("Desk").Get<DeskOptions>() ?? new();
if (desk.Mode is not ("sample" or "live")) throw new InvalidOperationException("Desk:Mode must be sample or live.");
var local = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
if (!local && (desk.EnableCollectors || desk.EnableLiveAi))
    throw new InvalidOperationException("Hosted database integration is deferred. Hosted collection is disabled.");
var databasePath = Path.GetFullPath(desk.DatabasePath.Replace("{mode}", desk.Mode), builder.Environment.ContentRootPath);
if (local) Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
using var databaseLock = builder.Environment.IsDevelopment()
    ? new FileStream(databasePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
    : null;
builder.Services.AddPooledDbContextFactory<DeskDb>(o => o.UseSqlite(
    $"Data Source={databasePath};Foreign Keys=True;Default Timeout=10"));
builder.Services.AddSingleton<CatalogStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AiLedger>();
builder.Services.AddSingleton<AiServiceCatalog>();
builder.Services.AddHttpClient<ISearchService, PerplexitySearchService>(c => c.Timeout = TimeSpan.FromSeconds(30))
    .RemoveAllLoggers().AddStandardResilienceHandler(o =>
    {
        o.Retry.DisableForUnsafeHttpMethods();
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
builder.Services.AddHttpClient<IDecisionService, TypeSafeDecisionService>(c => c.Timeout = TimeSpan.FromSeconds(30))
    .RemoveAllLoggers().AddStandardResilienceHandler(o =>
    {
        o.Retry.DisableForUnsafeHttpMethods();
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
builder.Services.AddSingleton<IIntelligenceModel, OpenAiServices>();
builder.Services.AddSingleton<SafeFetcher>();
builder.Services.AddSingleton<PublicationService>();
builder.Services.AddTransient<IntelligenceOrchestrator>();
builder.Services.AddTransient<SourceCollector>();
builder.Services.AddSingleton<RunQueue>();
builder.Services.AddHostedService<CollectionScheduler>();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_000_000);
builder.Services.AddFastEndpoints().AddJobQueues<JobRecord, JobStorage>().OpenApiDocument(o =>
{
    o.DocumentName = "v1";
    o.Title = "Signal Desk API";
    o.Version = "v1";
    o.EnableJWTBearerAuth = false;
    o.ConfigureOpenApi = options => options.AddSchemaTransformer(ContractSchemas.TransformAsync);
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(desk.AllowedOrigins)
    .WithMethods("GET", "POST", "PATCH").WithHeaders("Content-Type", "If-None-Match")
    .WithExposedHeaders("ETag")));

var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        if (!local && context.Request.Path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(503, "Hosted database configuration is deferred. Use local development.");
        await next(context);
    }
    catch (ApiException error)
    {
        await Results.Problem(statusCode: error.Status, title: error.Message).ExecuteAsync(context);
    }
    catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
    {
        await Results.Problem(statusCode: 504, title: "Operation timed out.").ExecuteAsync(context);
    }
    catch (Exception error) when (error is not OperationCanceledException)
    {
        app.Logger.LogError("Request failed with {ErrorType}; trace {TraceId}", error.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "An internal error occurred.").ExecuteAsync(context);
    }
});
app.UseStaticFiles();
app.UseCors();
app.UseFastEndpoints(o =>
{
    o.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.Errors.UseProblemDetails();
});
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (IDbContextFactory<DeskDb> factory, CancellationToken ct) =>
{
    if (!local) return Results.Problem(statusCode: 503, title: "Hosted persistence not configured.");
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any()
        ? Results.Ok(new { status = "ready", mode = desk.Mode }) : Results.Problem(statusCode: 503);
}).AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(o => o.WithTitle("Signal Desk API").AddDocument("v1").DisableAgent()).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/scalar/v1")).AllowAnonymous();
if (local)
{
    await using var scope = app.Services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DeskDb>>();
    await using var db = await factory.CreateDbContextAsync();
    await SeedData.InitializeAsync(db, desk.Mode);
}
if (local && desk.EnableCollectors)
    app.UseJobQueues(o => { o.MaxConcurrency = 1; o.ExecutionTimeLimit = TimeSpan.FromMinutes(5); });
app.Run();

public partial class Program;
