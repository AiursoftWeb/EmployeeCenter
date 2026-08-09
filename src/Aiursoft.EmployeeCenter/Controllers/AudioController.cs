using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.AudioViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class AudioController(
    EmployeeCenterDbContext context,
    AsrService asrService,
    AsrMediaProcessor mediaProcessor,
    ServiceTaskQueue taskQueue,
    StorageService storageService,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuthorizationService authorizationService,
    ILogger<AudioController> logger)
    : Controller
{
    private const int AudioPageSize = 50;

    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Audio",
        CascadedLinksIcon = "mic",
        CascadedLinksOrder = 6,
        LinkText = "Meeting Transcripts",
        LinkOrder = 1)]
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(page, 1);

        var isManager = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded;
        var userId = userManager.GetUserId(User);
        var userRoleIds = await GetUserRoleIdsAsync();

        IQueryable<Audio> query = context.Audios;
        if (!isManager)
        {
            query = query.Where(a =>
                a.OwnerId == userId ||
                a.AudioShares.Any(s =>
                    s.SharedWithUserId == userId ||
                    (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId))));
        }

        var totalAudioCount = await query.CountAsync();

        var audios = await query
            .OrderByDescending(a => a.CreateTime)
            .Skip((page - 1) * AudioPageSize)
            .Take(AudioPageSize + 1)
            .Select(a => new AudioListItemViewModel
            {
                Audio = a,
                HasTranscript = context.AudioAsrResults.Any(r => r.AudioId == a.Id && r.PlainText != ""),
                IsEmptyResult = context.AudioAsrResults.Any(r => r.AudioId == a.Id && r.PlainText == ""),
                HasMeetingMinutes = context.AudioAsrResults.Any(r => r.AudioId == a.Id && r.MeetingMinutesMarkdown != null && r.MeetingMinutesMarkdown != ""),
                MeetingMinutesAttemptCount = context.AudioAsrResults
                    .Where(r => r.AudioId == a.Id)
                    .Select(r => r.MeetingMinutesAttemptCount)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var hasNextPage = audios.Count > AudioPageSize;
        if (hasNextPage)
        {
            audios.RemoveAt(AudioPageSize);
        }

        var model = new IndexViewModel
        {
            Audios = audios,
            TotalAudioCount = totalAudioCount,
            Page = page,
            HasNextPage = hasNextPage
        };

        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageAudio)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageAudio)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (!TryGetExistingAudioPath(model.FilePath!, out var filePath))
            {
                ModelState.AddModelError(nameof(model.FilePath), "The file upload failed or the file is missing. Please re-upload.");
                return this.StackView(model);
            }
            var conversionResult = await ConvertVideoUploadToAudioAsync(filePath, nameof(model.FilePath));
            if (!conversionResult.Success)
            {
                return this.StackView(model);
            }
            filePath = conversionResult.AudioFilePath;

            var user = await userManager.GetUserAsync(User);
            var audio = new Audio
            {
                Name = model.Name,
                FilePath = filePath,
                CreateTime = DateTime.UtcNow,
                OwnerId = user!.Id
            };
            context.Audios.Add(audio);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return this.StackView(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();
        if (!await CanEditAudioAsync(audio)) return Unauthorized();

        return this.StackView(new EditViewModel
        {
            Id = audio.Id,
            Name = audio.Name,
            FilePath = audio.FilePath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (!ModelState.IsValid) return this.StackView(model);

        var audio = await context.Audios.FindAsync(model.Id);
        if (audio == null) return NotFound();
        if (!await CanEditAudioAsync(audio)) return Unauthorized();
        if (!TryGetExistingAudioPath(model.FilePath!, out var filePath))
        {
            ModelState.AddModelError(nameof(model.FilePath), "The file upload failed or the file is missing. Please re-upload.");
            return this.StackView(model);
        }
        var conversionResult = await ConvertVideoUploadToAudioAsync(filePath, nameof(model.FilePath));
        if (!conversionResult.Success)
        {
            return this.StackView(model);
        }
        filePath = conversionResult.AudioFilePath;

        var replaceAudio = audio.FilePath != filePath;
        if (replaceAudio)
        {
            try
            {
                await asrService.CancelActiveTaskAsync(audio);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    nameof(model.FilePath),
                    "The active ASR task could not be cancelled. No changes were made. Please try again.");
                DeleteVaultFileIfExists(conversionResult.ConvertedFilePath);
                return this.StackView(model);
            }
            var transcript = await context.AudioAsrResults.FindAsync(audio.Id);
            if (transcript != null) context.AudioAsrResults.Remove(transcript);
            var segments = await context.AudioAsrSegments
                .Where(segment => segment.AudioId == audio.Id)
                .ToListAsync();
            context.AudioAsrSegments.RemoveRange(segments);
            audio.AsrAttemptCount = 0;
            audio.EmptyResultCount = 0;
            audio.LastAsrAttemptTime = null;
            audio.AsrProcessingToken = Guid.NewGuid().ToString("N");
            audio.AsrActiveTaskId = null;
        }
        audio.Name = model.Name;
        audio.FilePath = filePath;

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Transcript), new { id = audio.Id });
    }

    public async Task<IActionResult> Transcript(int id)
    {
        var audio = await context.Audios.FirstOrDefaultAsync(a => a.Id == id);
        if (audio == null) return NotFound();
        if (!await CanViewAudioAsync(audio)) return NotFound();

        var canManageShares = await CanManageAudioAsync(audio);
        var permission = await GetAudioPermissionAsync(audio);

        var asrResult = await context.AudioAsrResults
            .AsNoTracking()
            .FirstOrDefaultAsync(result => result.AudioId == id);

        return this.StackView(new TranscriptViewModel
        {
            Audio = audio,
            PlainText = asrResult?.PlainText,
            MeetingMinutesMarkdown = asrResult?.MeetingMinutesMarkdown,
            MeetingMinutesAttemptCount = asrResult?.MeetingMinutesAttemptCount ?? 0,
            LastMeetingMinutesAttemptTime = asrResult?.LastMeetingMinutesAttemptTime,
            CanManageShares = canManageShares,
            Permission = permission!.Value
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAsr(int id)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();
        if (!await CanManageAudioAsync(audio)) return Unauthorized();

        try
        {
            await asrService.CancelActiveTaskAsync(audio);
        }
        catch (Exception)
        {
            TempData["AsrResetError"] = true;
            return RedirectToAction(nameof(Transcript), new { id });
        }
        var existingResult = await context.AudioAsrResults.FirstOrDefaultAsync(r => r.AudioId == id);
        if (existingResult != null)
        {
            context.AudioAsrResults.Remove(existingResult);
        }
        var segments = await context.AudioAsrSegments
            .Where(segment => segment.AudioId == id)
            .ToListAsync();
        context.AudioAsrSegments.RemoveRange(segments);

        audio.AsrAttemptCount = 0;
        audio.EmptyResultCount = 0;
        audio.LastAsrAttemptTime = null;
        audio.AsrProcessingToken = Guid.NewGuid().ToString("N");
        audio.AsrActiveTaskId = null;
        await context.SaveChangesAsync();

        // 将 ASR 处理放入独立后台任务，避免长耗时（最长 TimeoutSeconds）请求阻塞当前 HTTP 请求。
        taskQueue.QueueWithDependency<AsrService>(
            queueName: "asr",
            taskName: $"Reset ASR for audio {id}",
            task: svc => svc.ProcessAudioAsrAsync(id));

        TempData["AsrTaskQueued"] = true;
        return RedirectToAction(nameof(Transcript), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();
        if (!await CanManageAudioAsync(audio)) return Unauthorized();

        try
        {
            await asrService.CancelActiveTaskAsync(audio);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Deleting audio {AudioId} after its active ASR task could not be cancelled.",
                audio.Id);
        }
        context.Audios.Remove(audio);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int id)
    {
        var audio = await context.Audios.FirstOrDefaultAsync(a => a.Id == id);
        if (audio == null) return NotFound();
        if (!await CanViewAudioAsync(audio)) return NotFound();

        var plainText = await asrService.GetAsrResultByAudioIdAsync(id);
        if (string.IsNullOrEmpty(plainText))
        {
            return BadRequest("Transcript is empty or still processing.");
        }

        var fileBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var fileName = $"{audio.Name}.txt";
        return File(fileBytes, "text/plain", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> RawText(int id)
    {
        var audio = await context.Audios.FirstOrDefaultAsync(a => a.Id == id);
        if (audio == null) return NotFound();
        if (!await CanViewAudioAsync(audio)) return NotFound();

        var plainText = await asrService.GetAsrResultByAudioIdAsync(id);
        if (string.IsNullOrEmpty(plainText))
        {
            return Content(string.Empty, "text/plain");
        }

        return Content(plainText, "text/plain", System.Text.Encoding.UTF8);
    }

    public async Task<IActionResult> ManageShares(int id)
    {
        var audio = await context.Audios
            .Include(a => a.AudioShares)
            .ThenInclude(s => s.SharedWithUser)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (audio == null) return NotFound();
        if (!await CanManageAudioAsync(audio)) return Unauthorized();

        var allRoles = await roleManager.Roles.ToListAsync();

        return this.StackView(new ManageSharesViewModel
        {
            AudioId = audio.Id,
            AudioName = audio.Name,
            ExistingShares = audio.AudioShares.ToList(),
            AvailableRoles = allRoles
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShare(int id, AddShareViewModel model)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();
        if (!await CanManageAudioAsync(audio)) return Unauthorized();

        var targetCount = new[] { model.TargetUserId, model.TargetRoleId }.Count(targetId => !string.IsNullOrWhiteSpace(targetId));
        if (targetCount != 1)
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "invalid" });
        }

        if (model.TargetUserId != null && await userManager.FindByIdAsync(model.TargetUserId) == null)
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "invalid" });
        }
        if (model.TargetRoleId != null && await roleManager.FindByIdAsync(model.TargetRoleId) == null)
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "invalid" });
        }

        var exists = await context.AudioShares
            .AnyAsync(s => s.AudioId == id &&
                           ((model.TargetUserId != null && s.SharedWithUserId == model.TargetUserId) ||
                            (model.TargetRoleId != null && s.SharedWithRoleId == model.TargetRoleId)));
        if (exists)
        {
            return RedirectToAction(nameof(ManageShares), new { id, error = "duplicate" });
        }

        var share = new AudioShare
        {
            AudioId = id,
            SharedWithUserId = model.TargetUserId,
            SharedWithRoleId = model.TargetRoleId,
            Permission = model.Permission
        };

        context.AudioShares.Add(share);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageShares), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveShare(int id)
    {
        var share = await context.AudioShares
            .Include(s => s.Audio)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (share == null) return NotFound();
        if (!await CanManageAudioAsync(share.Audio)) return Unauthorized();

        var audioId = share.AudioId;
        context.AudioShares.Remove(share);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(ManageShares), new { id = audioId });
    }

    private async Task<bool> CanViewAudioAsync(Audio audio) => await GetAudioPermissionAsync(audio) != null;

    private async Task<bool> CanEditAudioAsync(Audio audio) => await GetAudioPermissionAsync(audio) == SharePermission.Editable;

    private async Task<SharePermission?> GetAudioPermissionAsync(Audio audio)
    {
        if ((await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded)
        {
            return SharePermission.Editable;
        }

        var userId = userManager.GetUserId(User);
        if (audio.OwnerId == userId) return SharePermission.Editable;

        var userRoleIds = await GetUserRoleIdsAsync();
        var share = await context.AudioShares
            .AnyAsync(s => s.AudioId == audio.Id &&
                           (s.SharedWithUserId == userId ||
                            (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId))));
        if (!share) return null;

        return await context.AudioShares
            .Where(s => s.AudioId == audio.Id)
            .Where(s => s.SharedWithUserId == userId ||
                        (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId)))
            .OrderByDescending(s => s.Permission)
            .Select(s => (SharePermission?)s.Permission)
            .FirstAsync();
    }

    private async Task<bool> CanManageAudioAsync(Audio audio)
    {
        if ((await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded)
        {
            return true;
        }

        var userId = userManager.GetUserId(User);
        return audio.OwnerId == userId;
    }

    private bool TryGetExistingAudioPath(string logicalPath, out string filePath)
    {
        filePath = string.Empty;
        try
        {
            var physicalPath = storageService.GetFilePhysicalPath(logicalPath, isVault: true);
            if (!System.IO.File.Exists(physicalPath)) return false;
            filePath = logicalPath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task<VideoAudioConversionResult> ConvertVideoUploadToAudioAsync(
        string filePath,
        string modelStateKey)
    {
        if (!IsVideoFile(filePath))
        {
            return new VideoAudioConversionResult(true, filePath, null);
        }

        var physicalVideoPath = storageService.GetFilePhysicalPath(filePath, isVault: true);
        var outputDirectory = Path.GetDirectoryName(physicalVideoPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            ModelState.AddModelError(modelStateKey, "The uploaded video could not be processed. Please upload an audio file or another video.");
            DeleteVaultFileIfExists(filePath);
            return new VideoAudioConversionResult(false, filePath, null);
        }

        try
        {
            var outputPrefix = $"{Path.GetFileNameWithoutExtension(filePath)}-audio-{Guid.NewGuid():N}";
            var physicalAudioPath = await mediaProcessor.ExtractAudioTrackAsync(
                physicalVideoPath,
                outputDirectory,
                outputPrefix);
            var logicalDirectory = Path.GetDirectoryName(filePath)?.Replace("\\", "/");
            var audioFilePath = string.IsNullOrEmpty(logicalDirectory)
                ? Path.GetFileName(physicalAudioPath)
                : $"{logicalDirectory}/{Path.GetFileName(physicalAudioPath)}";
            DeleteVaultFileIfExists(filePath);
            return new VideoAudioConversionResult(true, audioFilePath, audioFilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract audio track from uploaded video {FilePath}.", filePath);
            ModelState.AddModelError(modelStateKey, "The uploaded video audio track could not be extracted. Please upload an audio file or another video.");
            DeleteVaultFileIfExists(filePath);
            return new VideoAudioConversionResult(false, filePath, null);
        }
    }

    private static bool IsVideoFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteVaultFileIfExists(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            var physicalPath = storageService.GetFilePhysicalPath(filePath, isVault: true);
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Skipped deleting invalid vault file path {FilePath}.", filePath);
        }
    }

    private sealed record VideoAudioConversionResult(
        bool Success,
        string AudioFilePath,
        string? ConvertedFilePath);

    private async Task<List<string>> GetUserRoleIdsAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return [];

        var userRoles = await userManager.GetRolesAsync(user);
        return await roleManager.Roles
            .Where(r => userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();
    }
}
