using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

/// <summary>
/// Resolves audit targets through public DNS-over-HTTPS and connects to the
/// returned address directly. This deliberately bypasses Docker's embedded DNS,
/// which maps public service hostnames to the incoming proxy's private VIP.
/// </summary>
public sealed class PublicDnsResolver(IHttpClientFactory httpClientFactory)
{
    private const int DnsTypeA = 1;
    private const int DnsTypeAaaa = 28;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        if (IPAddress.TryParse(host, out var literalAddress))
        {
            return IsPublicAddress(literalAddress) ? [literalAddress] : [];
        }

        var queries = await Task.WhenAll(
            QueryAsync(host, DnsTypeA, cancellationToken),
            QueryAsync(host, DnsTypeAaaa, cancellationToken));

        return queries
            .SelectMany(addresses => addresses)
            .Where(IsPublicAddress)
            .Distinct()
            .OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ToList();
    }

    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolveAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Count == 0)
        {
            throw new HttpRequestException(
                $"Public DNS returned no public IP address for '{context.DnsEndPoint.Host}'.");
        }

        var failures = new List<Exception>();
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                failures.Add(ex);
                socket.Dispose();
            }
        }

        throw new HttpRequestException(
            $"None of the public IP addresses for '{context.DnsEndPoint.Host}' accepted a connection.",
            new AggregateException(failures));
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] is >= 64 and <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] is 0 or 168 => false,
                198 when bytes[1] is 18 or 19 or 51 => false,
                203 when bytes[1] == 0 && bytes[2] == 113 => false,
                >= 224 => false,
                _ => true
            };
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var isDocumentationPrefix = bytes[0] == 0x20 && bytes[1] == 0x01 &&
                                    bytes[2] == 0x0d && bytes[3] == 0xb8;
        return !address.IsIPv6LinkLocal &&
               !address.IsIPv6Multicast &&
               !address.IsIPv6SiteLocal &&
               !isDocumentationPrefix &&
               (bytes[0] & 0xfe) != 0xfc;
    }

    private async Task<IReadOnlyList<IPAddress>> QueryAsync(
        string host,
        int recordType,
        CancellationToken cancellationToken)
    {
        Exception cloudflareFailure;
        try
        {
            return await QueryProviderAsync(
                $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type={recordType}",
                recordType,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException)
        {
            cloudflareFailure = ex;
        }

        try
        {
            return await QueryProviderAsync(
                $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type={recordType}",
                recordType,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException)
        {
            throw new HttpRequestException(
                $"Both public DNS-over-HTTPS providers failed to resolve '{host}'.",
                new AggregateException(cloudflareFailure, ex));
        }
    }

    private async Task<IReadOnlyList<IPAddress>> QueryProviderAsync(
        string requestUri,
        int recordType,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("PublicDnsResolver");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/dns-json");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var dnsResponse = await JsonSerializer.DeserializeAsync<DnsJsonResponse>(
            stream,
            JsonOptions,
            cancellationToken);
        if (dnsResponse == null || dnsResponse.Status is not (0 or 3))
        {
            throw new InvalidDataException(
                $"The DNS-over-HTTPS provider returned status {dnsResponse?.Status.ToString() ?? "unknown"}.");
        }

        if (dnsResponse.Status == 3)
        {
            return [];
        }

        return (dnsResponse.Answer ?? [])
            .Where(answer => answer.Type == recordType)
            .Select(answer => IPAddress.TryParse(answer.Data, out var address) ? address : null)
            .OfType<IPAddress>()
            .ToList();
    }

    private sealed class DnsJsonResponse
    {
        public int Status { get; init; }
        public IReadOnlyList<DnsJsonAnswer>? Answer { get; init; }
    }

    private sealed class DnsJsonAnswer
    {
        public int Type { get; init; }
        public string Data { get; init; } = string.Empty;
    }
}
