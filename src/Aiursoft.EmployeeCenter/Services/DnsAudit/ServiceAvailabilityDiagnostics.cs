using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

/// <summary>
/// Request-local evidence shared with ConnectCallback. Never records headers,
/// response bodies, credentials or arbitrary exception messages.
/// </summary>
public sealed class ServiceAvailabilityDiagnostics
{
    public static readonly HttpRequestOptionsKey<ServiceAvailabilityDiagnostics> RequestKey =
        new("EmployeeCenter.ServiceAvailabilityDiagnostics");

    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly object _sync = new();
    private readonly List<string> _connections = [];
    private int _omittedConnections;
    private string _dns = "not completed";
    private string _stage = "DNS";
    private IPEndPoint? _target;
    private string? _connectionFailure;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public void Resolving()
    {
        lock (_sync) _stage = "DNS";
    }

    public void Resolved(IReadOnlyList<IPAddress> addresses, TimeSpan elapsed)
    {
        lock (_sync)
        {
            _dns = $"{elapsed.TotalMilliseconds:F0}ms [{string.Join(", ", addresses.Take(8))}]";
            _stage = "TLS/HTTP headers (no new TCP connection observed)";
        }
    }

    public void Connecting(IPEndPoint target)
    {
        lock (_sync)
        {
            _target = target;
            _connectionFailure = null;
            _stage = "TCP";
        }
    }

    public void ReceivedHeaders()
    {
        lock (_sync) _stage = "HTTP";
    }

    public void Connected(IPEndPoint remote, EndPoint? local, TimeSpan elapsed)
    {
        lock (_sync)
        {
            RecordConnection($"{remote} connected in {elapsed.TotalMilliseconds:F0}ms; local={local}");
            _stage = "TLS/HTTP headers";
        }
    }

    public void ConnectionFailed(IPEndPoint remote, TimeSpan elapsed, Exception error, bool requestCancelled)
    {
        var reason = error is OperationCanceledException
            ? requestCancelled ? "request deadline/cancellation" : "TCP connect timeout (4s)"
            : DescribeError(error);
        lock (_sync)
        {
            _connectionFailure = reason;
            RecordConnection($"{remote} failed in {elapsed.TotalMilliseconds:F0}ms: {reason}");
        }
    }

    private void RecordConnection(string evidence)
    {
        if (_connections.Count < 8) _connections.Add(evidence);
        else _omittedConnections++;
    }

    public string Describe(string outcome, Exception? error = null)
    {
        // ConnectCallback may still finish after the request deadline; snapshot its evidence atomically.
        lock (_sync)
        {
            var stage = _stage != "DNS" && error is HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }
                ? "TLS"
                : _stage;
            var cause = error == null ? string.Empty : $"; error={DescribeError(error)}";
            var failure = _connectionFailure ?? (error == null ? "none" : DescribeError(error));
            // DNS candidates do not prove which endpoint a pooled HTTP connection used.
            var connections = _connections.Count == 0 ? "not observed (pooled connection possible)" : string.Join(" | ", _connections);
            if (_omittedConnections > 0) connections += $" | {_omittedConnections} more connections omitted";
            return FormattableString.Invariant(
                $"UTC={StartedAt:O}; stage={stage}; target={_target}; failure={failure}; elapsed={_elapsed.Elapsed.TotalMilliseconds:F0}ms; {outcome}; TCP={connections}{cause}; DNS={_dns}");
        }
    }

    private static string DescribeError(Exception error)
    {
        return error switch
        {
            SocketException socket => $"SocketException/{socket.SocketErrorCode} (native={socket.NativeErrorCode})",
            AggregateException aggregate => string.Join("; ", aggregate.Flatten().InnerExceptions.Take(4).Select(DescribeError)),
            HttpRequestException http => $"HttpRequestException/{http.HttpRequestError}" +
                (http.StatusCode.HasValue ? $" HTTP={(int)http.StatusCode.Value}" : string.Empty) +
                (http.InnerException == null ? string.Empty : $" > {DescribeError(http.InnerException)}"),
            _ => error.GetType().Name + (error.InnerException == null ? string.Empty : $" > {DescribeError(error.InnerException)}")
        };
    }

    public static string Limit(string text, int length) =>
        text.Length <= length ? text : text[..(length - 3)] + "...";
}
