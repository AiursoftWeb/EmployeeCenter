using System.Net;
using Aiursoft.EmployeeCenter.Models.DnsAuditViewModels;

namespace Aiursoft.EmployeeCenter.Services.DnsAudit;

public static class DomainAliasRedirectEvaluator
{
    private static readonly HashSet<HttpStatusCode> RedirectStatusCodes =
    [
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.Found,
        HttpStatusCode.SeeOther,
        HttpStatusCode.TemporaryRedirect,
        HttpStatusCode.PermanentRedirect
    ];

    public static DomainAliasRedirectResult Evaluate(
        Uri sourceUri,
        string expectedTargetUrl,
        HttpStatusCode statusCode,
        Uri? location)
    {
        if (!RedirectStatusCodes.Contains(statusCode))
        {
            return new DomainAliasRedirectResult(
                false,
                (int)statusCode,
                null,
                $"Expected an HTTP redirect, but the alias returned HTTP {(int)statusCode} ({statusCode}).");
        }

        if (location == null)
        {
            return new DomainAliasRedirectResult(
                false,
                (int)statusCode,
                null,
                "The alias returned a redirect status without a Location header.");
        }

        if (!Uri.TryCreate(sourceUri, location, out var actualTarget))
        {
            return new DomainAliasRedirectResult(
                false,
                (int)statusCode,
                location.ToString(),
                $"The redirect Location '{location}' is not a valid target URL.");
        }

        if (!TryNormalizeTargetUrl(expectedTargetUrl, out var expectedTarget, out var validationError))
        {
            return new DomainAliasRedirectResult(
                false,
                (int)statusCode,
                actualTarget.AbsoluteUri,
                $"The registered target URL is invalid. {validationError}");
        }

        var normalizedActual = NormalizeAbsoluteUri(actualTarget);
        if (!string.Equals(expectedTarget, normalizedActual, StringComparison.Ordinal))
        {
            return new DomainAliasRedirectResult(
                false,
                (int)statusCode,
                normalizedActual,
                $"The alias redirects to '{normalizedActual}', but EmployeeCenter expects exactly '{expectedTarget}'.");
        }

        return new DomainAliasRedirectResult(
            true,
            (int)statusCode,
            normalizedActual,
            $"HTTP {(int)statusCode} redirects exactly to '{expectedTarget}'.");
    }

    public static bool TryNormalizeTargetUrl(string? value, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Enter an absolute HTTPS URL.";
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "The target URL must use HTTPS.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "The target URL must contain a public hostname and must not contain credentials.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            error = "The target URL must not contain a fragment.";
            return false;
        }

        normalized = NormalizeAbsoluteUri(uri);
        return true;
    }

    private static string NormalizeAbsoluteUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
            Fragment = string.Empty
        };
        if (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443)
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri;
    }
}
