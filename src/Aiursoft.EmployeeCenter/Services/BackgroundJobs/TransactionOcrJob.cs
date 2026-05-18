using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class TransactionOcrJob(
    EmployeeCenterDbContext db,
    OcrService ocrService,
    IOptions<OcrSettings> ocrSettings,
    ILogger<TransactionOcrJob> logger) : IBackgroundJob
{
    private readonly OcrSettings _ocrSettings = ocrSettings.Value;

    public string Name => "Transaction OCR Job";
    public string Description => "Scans for transactions with attachments that haven't been OCR processed yet and performs OCR recognition.";

    public async Task ExecuteAsync()
    {
        if (!_ocrSettings.Enabled)
        {
            logger.LogInformation("Transaction OCR job skipped because OCR is disabled in configuration.");
            return;
        }

        try
        {
            logger.LogInformation("Transaction OCR job started");

            // Find transactions that have attachments but are missing at least one OCR result for those attachments
            var unprocessedTransactionIds = await db.Transactions
                .Where(t =>
                    (!string.IsNullOrEmpty(t.InvoicePath) && !db.TransactionOcrResults.Any(r => r.TransactionId == t.Id && r.AttachmentType == TransactionAttachmentType.Invoice)) ||
                    (!string.IsNullOrEmpty(t.MT103Path) && !db.TransactionOcrResults.Any(r => r.TransactionId == t.Id && r.AttachmentType == TransactionAttachmentType.MT103)) ||
                    (!string.IsNullOrEmpty(t.PaymentVoucherPath) && !db.TransactionOcrResults.Any(r => r.TransactionId == t.Id && r.AttachmentType == TransactionAttachmentType.PaymentVoucher))
                )
                .Where(t => t.OcrAttemptCount < _ocrSettings.TransactionOcrMaxRetryCount)
                .OrderBy(t => t.OcrAttemptCount)
                .ThenByDescending(t => t.TransactionTime)
                .Select(t => t.Id)
                .Take(50)
                .ToListAsync();

            if (unprocessedTransactionIds.Count == 0)
            {
                logger.LogInformation("No unprocessed transactions found.");
                return;
            }

            logger.LogInformation("Found {Count} transactions with unprocessed attachments. Starting OCR processing...", unprocessedTransactionIds.Count);

            foreach (var transactionId in unprocessedTransactionIds)
            {
                await ocrService.ProcessTransactionOcrAsync(transactionId);
            }

            logger.LogInformation("Transaction OCR job completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in transaction OCR job");
        }
    }
}
