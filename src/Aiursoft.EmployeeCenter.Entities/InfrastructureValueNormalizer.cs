using System.Globalization;
using System.Net;
using System.Text;

namespace Aiursoft.EmployeeCenter.Entities;

public static class InfrastructureValueNormalizer
{
    private static readonly IdnMapping Idn = new() { UseStd3AsciiRules = true };

    public static string NormalizeDomain(string value)
    {
        var candidate = value.Trim().TrimEnd('.');
        if (candidate.Length == 0)
        {
            throw new FormatException("A domain cannot be empty.");
        }

        try
        {
            var labels = candidate.Split('.').Select(label => Idn.GetAscii(label)).ToArray();
            var ascii = string.Join('.', labels);
            if (ascii.Length > 253 || labels.Any(label =>
                    label.Length is 0 or > 63 ||
                    !char.IsAsciiLetterOrDigit(label[0]) ||
                    !char.IsAsciiLetterOrDigit(label[^1]) ||
                    label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
            {
                throw new FormatException("The domain is not a valid DNS name.");
            }

            return ascii.ToLowerInvariant();
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The domain is not a valid DNS name.", exception);
        }
    }

    public static string? NormalizeOptionalHostname(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeDomain(value);

    public static string? NormalizeOptionalIp(string? value, System.Net.Sockets.AddressFamily family)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!IPAddress.TryParse(value.Trim(), out var address) || address.AddressFamily != family)
        {
            throw new FormatException(family == System.Net.Sockets.AddressFamily.InterNetwork
                ? "The IPv4 address is invalid."
                : "The IPv6 address is invalid.");
        }

        return address.ToString();
    }

    public static string NormalizeName(string value) =>
        value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}
