namespace Aiursoft.EmployeeCenter.Configuration;

public sealed class AlertCenterSettings
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://alertcenter.aiursoft.com";
    public string ApiKey { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = "https://employee.aiursoft.com";
}
