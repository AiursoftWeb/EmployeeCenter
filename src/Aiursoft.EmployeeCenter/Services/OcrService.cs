using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace Aiursoft.EmployeeCenter.Services;

public class OcrResponse
{
    public string Status { get; set; } = string.Empty;
    public double DurationS { get; set; }
    public string Device { get; set; } = string.Empty;
    public List<OcrResultItem>? Results { get; set; }
    public string? Error { get; set; }
}

public class OcrResultItem
{
    public List<List<double>> Points { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    [JsonProperty("page_num")]
    public int PageNum { get; set; }
}

public class OcrService(
    HttpClient httpClient,
    IOptions<OcrSettings> ocrSettings,
    EmployeeCenterDbContext dbContext,
    StorageService storageService,
    ILogger<OcrService> logger) : ITransientDependency
{
    private readonly OcrSettings _ocrSettings = ocrSettings.Value;

    private async Task<(string JsonResult, string PlainText)?> RecognizeAsync(string filePath)
    {
        if (string.IsNullOrEmpty(_ocrSettings.Endpoint) || string.IsNullOrEmpty(_ocrSettings.BearerToken))
        {
            logger.LogWarning("OCR settings are not configured.");
            return null;
        }

        if (!File.Exists(filePath))
        {
            logger.LogError("File not found at {FilePath}", filePath);
            return null;
        }

        using var form = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".pdf")
        {
            logger.LogInformation("File extension {Extension} is not supported. Skipping OCR.", extension);
            return null;
        }

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var request = new HttpRequestMessage(HttpMethod.Post, _ocrSettings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ocrSettings.BearerToken);
        request.Content = form;

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var ocrResponse = JsonConvert.DeserializeObject<OcrResponse>(content);
            if (ocrResponse?.Status == "ok")
            {
                var plainText = ocrResponse.Results != null
                    ? string.Join("\n", ocrResponse.Results.Select(r => r.Text))
                    : string.Empty;
                return (content, plainText);
            }

            logger.LogError("OCR API returned error status: {Status}, Error: {Error}",
                ocrResponse?.Status, ocrResponse?.Error);
        }
        else
        {
            logger.LogError("OCR API request failed with status {StatusCode}: {Content}",
                response.StatusCode, content);
        }

        return null;
    }

    public async Task ProcessContractOcrAsync(int contractId)
    {
        if (!_ocrSettings.Enabled)
        {
            return;
        }

        var contract = await dbContext.Contracts.FindAsync(contractId);
        if (contract == null)
        {
            logger.LogWarning("Contract with ID {ContractId} not found for OCR processing", contractId);
            return;
        }

        contract.OcrAttemptCount++;
        contract.LastOcrAttemptTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        try
        {
            var filePath = storageService.GetFilePhysicalPath(contract.FilePath, isVault: true);
            var result = await RecognizeAsync(filePath);
            if (result != null)
            {
                if (string.IsNullOrWhiteSpace(result.Value.PlainText))
                {
                    contract.EmptyResultCount++;
                    await dbContext.SaveChangesAsync();

                    if (contract.EmptyResultCount >= _ocrSettings.ContractOcrMaxEmptyRetryCount)
                    {
                        var ocrResult = new ContractOcrResult
                        {
                            ContractId = contractId,
                            JsonResult = result.Value.JsonResult,
                            PlainText = string.Empty
                        };
                        dbContext.ContractOcrResults.Add(ocrResult);
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation("Contract {ContractId} OCR returned empty results {AttemptCount} times. Marking as permanently empty.", contractId, contract.EmptyResultCount);
                    }
                }
                else
                {
                    contract.EmptyResultCount = 0;

                    var existing = await dbContext.ContractOcrResults
                        .FirstOrDefaultAsync(r => r.ContractId == contractId);
                    if (existing != null)
                        dbContext.ContractOcrResults.Remove(existing);

                    var ocrResult = new ContractOcrResult
                    {
                        ContractId = contractId,
                        JsonResult = result.Value.JsonResult,
                        PlainText = result.Value.PlainText
                    };
                    dbContext.ContractOcrResults.Add(ocrResult);
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Successfully processed OCR for contract {ContractId}", contractId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing OCR for contract {ContractId}", contractId);
        }
    }

    public async Task ProcessTransactionOcrAsync(int transactionId)
    {
        if (!_ocrSettings.Enabled)
        {
            return;
        }

        var transaction = await dbContext.Transactions.FindAsync(transactionId);
        if (transaction == null)
        {
            logger.LogWarning("Transaction with ID {TransactionId} not found for OCR processing", transactionId);
            return;
        }

        transaction.OcrAttemptCount++;
        transaction.LastOcrAttemptTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var attachments = new List<(string? Path, TransactionAttachmentType Type)>
        {
            (transaction.InvoicePath, TransactionAttachmentType.Invoice),
            (transaction.MT103Path, TransactionAttachmentType.MT103),
            (transaction.PaymentVoucherPath, TransactionAttachmentType.PaymentVoucher)
        };

        var hadEmptyResult = false;
        var hadTextResult = false;

        foreach (var (path, type) in attachments)
        {
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            var exists = await dbContext.TransactionOcrResults
                .AnyAsync(r => r.TransactionId == transactionId && r.AttachmentType == type);
            if (exists)
            {
                continue;
            }

            try
            {
                var filePath = storageService.GetFilePhysicalPath(path, isVault: true);
                var result = await RecognizeAsync(filePath);
                if (result != null)
                {
                    if (string.IsNullOrWhiteSpace(result.Value.PlainText))
                    {
                        hadEmptyResult = true;

                        if (transaction.EmptyResultCount + 1 >= _ocrSettings.TransactionOcrMaxEmptyRetryCount)
                        {
                            var ocrResult = new TransactionOcrResult
                            {
                                TransactionId = transactionId,
                                AttachmentType = type,
                                JsonResult = result.Value.JsonResult,
                                PlainText = string.Empty
                            };
                            dbContext.TransactionOcrResults.Add(ocrResult);
                            await dbContext.SaveChangesAsync();
                            logger.LogInformation("Transaction {TransactionId} attachment {Type} OCR returned empty results at threshold. Marking as permanently empty.", transactionId, type);
                        }
                    }
                    else
                    {
                        hadTextResult = true;

                        var existing = await dbContext.TransactionOcrResults
                            .FirstOrDefaultAsync(r => r.TransactionId == transactionId && r.AttachmentType == type);
                        if (existing != null)
                            dbContext.TransactionOcrResults.Remove(existing);

                        var ocrResult = new TransactionOcrResult
                        {
                            TransactionId = transactionId,
                            AttachmentType = type,
                            JsonResult = result.Value.JsonResult,
                            PlainText = result.Value.PlainText
                        };
                        dbContext.TransactionOcrResults.Add(ocrResult);
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation("Successfully processed OCR for transaction {TransactionId} attachment {Type}", transactionId, type);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing OCR for transaction {TransactionId} attachment {Type}", transactionId, type);
            }
        }

        if (hadTextResult)
        {
            transaction.EmptyResultCount = 0;
        }
        else if (hadEmptyResult)
        {
            transaction.EmptyResultCount++;
        }
        if (hadTextResult || hadEmptyResult)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<string?> GetOcrResultByContractIdAsync(int contractId)
    {
        var result = await dbContext.ContractOcrResults
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefaultAsync();

        return result?.JsonResult;
    }
}
