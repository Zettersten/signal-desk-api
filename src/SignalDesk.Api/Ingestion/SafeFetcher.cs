using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using SignalDesk.Api.AI;
using SignalDesk.Api.Configuration;
using SignalDesk.Api.Contracts;
using SignalDesk.Api.Data;

namespace SignalDesk.Api.Ingestion;

public sealed class SafeFetcher
{
    public static string Host(string value) => new Uri(value).IdnHost.ToLowerInvariant().Removeprefix();

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xe0) == 0x20 &&
                !(b[0] == 0x20 && b[1] == 2) &&
                !(b[0] == 0x20 && b[1] == 1 && ((b[2] == 0 && b[3] == 0) || (b[2] == 0x0d && b[3] == 0xb8)));
        return b[0] is not (0 or 10 or 127) && b[0] < 224 &&
            !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31) &&
            !(b[0] == 192 && (b[1] == 168 || (b[1] == 0 && b[2] is 0 or 2))) &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) &&
            !(b[0] == 198 && (b[1] is 18 or 19 || (b[1] == 51 && b[2] == 100))) &&
            !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }

    public static Uri Validate(string address, string approvedUrl)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, "https") ||
            uri.Port != 443 || uri.UserInfo.Length > 0 || Uri.CheckHostName(uri.IdnHost) != UriHostNameType.Dns ||
            !string.Equals(Host(address), Host(approvedUrl)))
            throw new ApiException(400, "URL is outside the approved HTTPS source.");
        return uri;
    }

    public async Task<FetchedPage> FetchAsync(string address, string approvedUrl, CancellationToken ct)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, token) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
                if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
                    throw new ApiException(400, "Source resolved to a nonpublic destination.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SignalDesk/1.0 (+source-approved-collection)");
        for (var redirects = 0; redirects <= 3; redirects++)
        {
            var uri = Validate(address, approvedUrl);
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                address = response.Headers.Location is { } location ? new Uri(uri, location).AbsoluteUri :
                    throw new ApiException(503, "Invalid source redirect.");
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new ApiException((int)response.StatusCode is 400 or 401 or 403 or 404 or 410 ? 422 : 503,
                    $"Source retrieval failed ({(int)response.StatusCode}).");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(bytes, ct)) > 0)
            {
                if (buffer.Length + read > 2_000_000) throw new ApiException(422, "Article exceeds the retrieval size limit.");
                buffer.Write(bytes, 0, read);
            }
            return new(System.Text.Encoding.UTF8.GetString(buffer.ToArray()), response.Content.Headers.ContentType?.MediaType ?? "");
        }
        throw new ApiException(503, "Too many source redirects.");
    }

    public static SearchHit[] ParseFeed(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new()
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
        var root = XDocument.Load(reader);
        return root.Descendants().Where(e => e.Name.LocalName is "item" or "entry").Take(50).Select(e =>
        {
            string Value(params string[] names) => e.Elements().FirstOrDefault(x => names.Contains(x.Name.LocalName))?.Value ?? "";
            var link = e.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "link") &&
                ((string?)x.Attribute("rel") is null or "alternate"));
            return new SearchHit(Value("title"), (string?)link?.Attribute("href") ?? link?.Value ?? "",
                Value("description", "summary", "content"), Value("pubDate", "published", "updated"));
        }).Where(x => x.Url.Length > 0).ToArray();
    }

    public static DocumentRow ParseArticle(string html, string sourceId, string url, string fallbackTitle, DateTime now)
    {
        var page = new HtmlParser().ParseDocument(html);
        foreach (var node in page.QuerySelectorAll("script,style,nav,footer,header,form,aside")) node.Remove();
        var paragraphs = (page.QuerySelector("article") ?? page.QuerySelector("main"))
            ?.QuerySelectorAll("p").Select(p => p.TextContent.Trim()).Where(p => p.Length > 25)
            .Distinct(StringComparer.Ordinal).Take(80).Select((text, i) => new Paragraph($"p-{i}-{CatalogStore.Hash(text)[..12]}", text)).ToArray() ?? [];
        var rawDate = page.QuerySelector("meta[property='article:published_time']")?.GetAttribute("content") ??
            page.QuerySelector("time[datetime]")?.GetAttribute("datetime");
        var hasTime = rawDate?.Contains('T') == true && (rawDate.EndsWith('Z') || rawDate.Contains('+') || rawDate.Length >= 25);
        DateTime? date = hasTime && DateTimeOffset.TryParse(rawDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var parsed) ? parsed.UtcDateTime : null;
        var content = Wire.Encode(paragraphs);
        return new()
        {
            Id = $"doc-{CatalogStore.Hash(url + content)}",
            SourceId = sourceId,
            CanonicalUrl = url,
            Title = page.QuerySelector("h1")?.TextContent.Trim() ?? fallbackTitle,
            PublishedUtc = date,
            DatePrecision = date is null ? "unknown" : "instant",
            FetchedUtc = now,
            ContentHash = CatalogStore.Hash(content),
            ParagraphsJson = content,
            Availability = paragraphs.Length == 0 ? "unavailable" : "article"
        };
    }
}
