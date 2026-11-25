using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SignalDesk.Api.Configuration;

public sealed class LocalAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IOptions<DeskOptions> desk, IWebHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var address = Context.Connection.RemoteIpAddress;
        var trustedHost = new[] { "localhost", "127.0.0.1", "::1", "[::1]" }
            .Contains(Request.Host.Host, StringComparer.OrdinalIgnoreCase);
        var origin = Request.Headers.Origin.ToString();
        var sameOrigin = Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
            uri.Scheme == Request.Scheme;
        if (!trustedHost || (origin.Length > 0 && !sameOrigin && !desk.Value.AllowedOrigins.Contains(origin, StringComparer.Ordinal)) ||
            (address is null && !environment.IsEnvironment("Testing")) ||
            (address is not null && !IPAddress.IsLoopback(address)))
            return Task.FromResult(AuthenticateResult.Fail("Development access requires loopback."));
        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, "local-developer"), new(ClaimTypes.Role, "Reader"), new(ClaimTypes.Role, "Admin")],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
