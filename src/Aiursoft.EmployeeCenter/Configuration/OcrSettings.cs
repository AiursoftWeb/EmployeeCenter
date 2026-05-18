namespace Aiursoft.EmployeeCenter.Configuration;

public class OcrSettings
{
    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? BearerToken { get; init; }
    public int ContractOcrMaxRetryCount { get; init; } = 5;
    public int TransactionOcrMaxRetryCount { get; init; } = 10;
}
