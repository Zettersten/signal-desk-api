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
