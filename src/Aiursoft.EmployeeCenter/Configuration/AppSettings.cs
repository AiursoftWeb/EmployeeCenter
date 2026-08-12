namespace Aiursoft.EmployeeCenter.Configuration;

public class AppSettings
{
    public required string AuthProvider { get; init; } = "Local";
    public bool LocalEnabled => AuthProvider == "Local";
    public bool OIDCEnabled => AuthProvider == "OIDC";

    public required OidcSettings OIDC { get; init; }
    public required LocalSettings Local { get; init; }
    public required OcrSettings OCR { get; init; }

    /// <summary>
    /// The name of the company. Used in certificates.
    /// </summary>
    public string CompanyName { get; init; } = "Aiursoft";

    /// <summary>
    /// Keep the user sign in after the browser is closed.
    /// </summary>
    public bool PersistsSignIn { get; init; }

    /// <summary>
    /// Automatically assign the user to this role when they log in.
    /// </summary>
    public string? DefaultRole { get; init; } = string.Empty;

    /// <summary>
    /// The path to export data to.
    /// </summary>
    public string ExportPath { get; init; } = "/tmp/export";

    public required AgentSettings Agent { get; init; }
}

public class AgentSettings
{
    public required string Endpoint { get; init; }
    public int MeetingMinutesTimeoutSeconds { get; init; } = 600;
    public int MeetingMinutesMaxRetryCount { get; init; } = 5;
}
