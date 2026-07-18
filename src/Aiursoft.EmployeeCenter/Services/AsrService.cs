using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.Scanner.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace Aiursoft.EmployeeCenter.Services;

public class AsrResponse
{
    public string? Text { get; set; }
    public string? Error { get; set; }
}

public class AsrService(
    HttpClient httpClient,
    IOptions<AsrSettings> asrSettings,
    EmployeeCenterDbContext dbContext,
    StorageService storageService,
    ILogger<AsrService> logger) : ITransientDependency
{
    private readonly AsrSettings _asrSettings = asrSettings.Value;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac", ".webm", ".amr"
    };

    // Returns the transcribed plain text, string.Empty when the audio was processed but produced no text,
    // or null when the file cannot be processed (missing, unsupported, or upstream failure).
    private async Task<string?> RecognizeAsync(string filePath)
    {
        if (string.IsNullOrEmpty(_asrSettings.Endpoint) || string.IsNullOrEmpty(_asrSettings.BearerToken))
        {
            logger.LogWarning("ASR settings are not configured.");
            return null;
        }

        if (!File.Exists(filePath))
        {
            logger.LogError("File not found at {FilePath}", filePath);
            return null;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            logger.LogInformation("File extension {Extension} is not a supported audio format. Skipping ASR.", extension);
            return null;
        }

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_asrSettings.Model), "model");
        if (!string.IsNullOrEmpty(_asrSettings.Level))
        {
            form.Add(new StringContent(_asrSettings.Level), "level");
        }
        if (!string.IsNullOrEmpty(_asrSettings.Language))
        {
            form.Add(new StringContent(_asrSettings.Language), "language");
        }

        using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var request = new HttpRequestMessage(HttpMethod.Post, _asrSettings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _asrSettings.BearerToken);
        request.Content = form;

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("ASR API request failed with status {StatusCode}: {Content}", response.StatusCode, content);
            return null;
        }

        var asrResponse = JsonConvert.DeserializeObject<AsrResponse>(content);
        if (!string.IsNullOrEmpty(asrResponse?.Error))
        {
            logger.LogError("ASR API returned error: {Error}", asrResponse.Error);
            return null;
        }

        return asrResponse?.Text ?? string.Empty;
    }

    public async Task ProcessAudioAsrAsync(int audioId)
    {
        if (!_asrSettings.Enabled)
        {
            return;
        }

        var audio = await dbContext.Audios.FindAsync(audioId);
        if (audio == null)
        {
            logger.LogWarning("Audio with ID {AudioId} not found for ASR processing", audioId);
            return;
        }

        audio.AsrAttemptCount++;
        audio.LastAsrAttemptTime = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        try
        {
            var filePath = storageService.GetFilePhysicalPath(audio.FilePath, isVault: true);
            var plainText = await RecognizeAsync(filePath);
            if (plainText == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(plainText))
            {
                audio.EmptyResultCount++;
                await dbContext.SaveChangesAsync();

                if (audio.EmptyResultCount >= _asrSettings.AsrMaxEmptyRetryCount)
                {
                    var emptyAsrResult = new AudioAsrResult
                    {
                        AudioId = audioId,
                        PlainText = string.Empty
                    };
                    dbContext.AudioAsrResults.Add(emptyAsrResult);
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Audio {AudioId} ASR returned empty results {AttemptCount} times. Marking as permanently empty.", audioId, audio.EmptyResultCount);
                }

                return;
            }

            audio.EmptyResultCount = 0;

            var existing = await dbContext.AudioAsrResults
                .FirstOrDefaultAsync(r => r.AudioId == audioId);
            if (existing != null)
                dbContext.AudioAsrResults.Remove(existing);

            var asrResult = new AudioAsrResult
            {
                AudioId = audioId,
                PlainText = plainText
            };
            dbContext.AudioAsrResults.Add(asrResult);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Successfully processed ASR for audio {AudioId}", audioId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing ASR for audio {AudioId}", audioId);
        }
    }

    public async Task<string?> GetAsrResultByAudioIdAsync(int audioId)
    {
        var result = await dbContext.AudioAsrResults
            .Where(r => r.AudioId == audioId)
            .OrderByDescending(r => r.CreateTime)
            .FirstOrDefaultAsync();

        return result?.PlainText;
    }
}
