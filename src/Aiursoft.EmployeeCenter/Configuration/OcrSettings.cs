namespace Aiursoft.EmployeeCenter.Configuration;

public class OcrSettings
{
    public bool Enabled { get; init; } = true;
    public string? Endpoint { get; init; }
    public string? BearerToken { get; init; }
    public int TimeoutSeconds { get; init; } = 1800;
    public int ContractOcrMaxRetryCount { get; init; } = 30;
    public int ContractOcrMaxEmptyRetryCount { get; init; } = 10;
    public int TransactionOcrMaxRetryCount { get; init; } = 30;
    public int TransactionOcrMaxEmptyRetryCount { get; init; } = 10;
}
