using Aiursoft.Canon.BackgroundJobs;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services.BackgroundJobs;

public class AudioAsrJob(
    EmployeeCenterDbContext db,
    AsrService asrService,
    IOptions<AsrSettings> asrSettings,
    ILogger<AudioAsrJob> logger) : IBackgroundJob
{
    private readonly AsrSettings _asrSettings = asrSettings.Value;

    public string Name => "Audio ASR Job";
    public string Description => "Scans for audio recordings that haven't been transcribed yet and performs speech-to-text recognition.";

    public async Task ExecuteAsync()
    {
        if (!_asrSettings.Enabled)
        {
            logger.LogInformation("Audio ASR job skipped because ASR is disabled in configuration.");
            return;
        }

        try
        {
            logger.LogInformation("Audio ASR job started");

            // Find audio that still needs transcription:
            // - No non-empty ASR result exists yet
            // - Has not exceeded AsrAttemptCount limit (safety valve for crashes)
            // - Has not exceeded EmptyResultCount limit (truly silent audio)
            var unprocessedAudioIds = await db.Audios
                .Where(a => !db.AudioAsrResults.Any(r => r.AudioId == a.Id && r.PlainText != ""))
                .Where(a => a.AsrAttemptCount < _asrSettings.AsrMaxRetryCount)
                .Where(a => a.EmptyResultCount < _asrSettings.AsrMaxEmptyRetryCount)
                .OrderBy(a => a.AsrAttemptCount)
                .ThenByDescending(a => a.CreateTime)
                .Select(a => a.Id)
                .Take(50)
                .ToListAsync();

            if (unprocessedAudioIds.Count == 0)
            {
                logger.LogInformation("No unprocessed audio found.");
                return;
            }

            logger.LogInformation("Found {Count} unprocessed audio files. Starting ASR processing...", unprocessedAudioIds.Count);

            var failedAudioIds = new List<int>();
            foreach (var audioId in unprocessedAudioIds)
            {
                try
                {
                    await asrService.ProcessAudioAsrAsync(audioId);
                }
                catch (Exception ex)
                {
                    failedAudioIds.Add(audioId);
                    logger.LogError(ex, "ASR processing failed for audio {AudioId}.", audioId);
                }
            }
            if (failedAudioIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"ASR processing failed for audio IDs: {string.Join(", ", failedAudioIds)}.");
            }

            logger.LogInformation("Audio ASR job completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in audio ASR job");
            throw;
        }
    }
}
