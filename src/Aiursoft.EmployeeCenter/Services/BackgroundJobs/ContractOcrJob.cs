using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class ContractOcrJob(
    EmployeeCenterDbContext db,
    OcrService ocrService,
    IOptions<OcrSettings> ocrSettings,
    ILogger<ContractOcrJob> logger) : IBackgroundJob
{
    private readonly OcrSettings _ocrSettings = ocrSettings.Value;

    public string Name => "Contract OCR Job";
    public string Description => "Scans for contracts that haven't been OCR processed yet and performs OCR recognition.";

    public async Task ExecuteAsync()
    {
        if (!_ocrSettings.Enabled)
        {
            logger.LogInformation("Contract OCR job skipped because OCR is disabled in configuration.");
            return;
        }

        try
        {
            logger.LogInformation("Contract OCR job started");
            
            // Find contracts that still need OCR processing:
            // - No non-empty OCR result exists yet
            // - Has not exceeded OcrAttemptCount limit (safety valve for crashes)
            // - Has not exceeded EmptyResultCount limit (truly empty PDFs)
            var unprocessedContractIds = await db.Contracts
                .Where(c => !db.ContractOcrResults.Any(r => r.ContractId == c.Id && r.PlainText != ""))
                .Where(c => c.OcrAttemptCount < _ocrSettings.ContractOcrMaxRetryCount)
                .Where(c => c.EmptyResultCount < _ocrSettings.ContractOcrMaxEmptyRetryCount)
                .OrderBy(c => c.OcrAttemptCount)
                .ThenByDescending(c => c.CreateTime)
                .Select(c => c.Id)
                .Take(50)
                .ToListAsync();

            if (unprocessedContractIds.Count == 0)
            {
                logger.LogInformation("No unprocessed contracts found.");
                return;
            }

            logger.LogInformation("Found {Count} unprocessed contracts. Starting OCR processing...", unprocessedContractIds.Count);

            foreach (var contractId in unprocessedContractIds)
            {
                await ocrService.ProcessContractOcrAsync(contractId);
            }

            logger.LogInformation("Contract OCR job completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in contract OCR job");
        }
    }
}
