using Aiursoft.Canon.TaskQueue;
using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.AudioViewModels;
using Aiursoft.EmployeeCenter.Services;
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
    ServiceTaskQueue taskQueue,
    AudioMediaQueueService mediaQueueService,
    AudioFileCleanupService fileCleanupService,
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
            var user = await userManager.GetUserAsync(User);
            var upload = await GetConsumableUploadAsync(
                model.UploadId!.Value,
                user!.Id,
                AudioUploadPurpose.Create,
                targetAudioId: null);
            if (upload == null)
            {
                ModelState.AddModelError(nameof(model.UploadId), "The upload is invalid, expired, or has already been used. Please re-upload.");
                return this.StackView(model);
            }
            var audio = new Audio
            {
                Name = model.Name,
                FilePath = upload.FilePath,
                MediaStatus = AudioMediaStatus.Uploaded,
                CreateTime = DateTime.UtcNow,
                OwnerId = user!.Id
            };
            context.Audios.Add(audio);
            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                context.ChangeTracker.Clear();
                ModelState.AddModelError(nameof(model.UploadId), "The upload has already been assigned to a recording.");
                return this.StackView(model);
            }
            QueueMediaProcessing(audio.Id);
            return RedirectToAction(nameof(Transcript), new { id = audio.Id });
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
            UploadId = null
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
        audio.Name = model.Name;
        if (model.UploadId == null)
        {
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Transcript), new { id = audio.Id });
        }
        if (audio.MediaStatus == AudioMediaStatus.Processing)
        {
            ModelState.AddModelError(nameof(model.UploadId), "Another recording replacement is already being processed.");
            return this.StackView(model);
        }
        var userId = userManager.GetUserId(User)!;
        var upload = await GetConsumableUploadAsync(
            model.UploadId.Value,
            userId,
            AudioUploadPurpose.Replace,
            audio.Id);
        if (upload == null)
        {
            ModelState.AddModelError(nameof(model.UploadId), "The upload is invalid, expired, or has already been used. Please re-upload.");
            return this.StackView(model);
        }
        var abandonedPendingPath = audio.PendingFilePath;
        audio.PendingFilePath = upload.FilePath;
        audio.MediaStatus = AudioMediaStatus.Uploaded;
        audio.MediaProcessingError = null;
        audio.MediaProcessingToken = Guid.NewGuid().ToString("N");
        fileCleanupService.QueueDeletion(abandonedPendingPath);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
        {
            context.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(model.UploadId), "The upload has already been used or assigned to another recording.");
            return this.StackView(model);
        }
        await fileCleanupService.TryCleanupQueuedAsync();
        QueueMediaProcessing(audio.Id);
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
        audio.AsrTerminalError = null;
        await context.SaveChangesAsync();

        // 将 ASR 处理放入独立后台任务，避免长耗时（最长 TimeoutSeconds）请求阻塞当前 HTTP 请求。
        var asrProcessingToken = audio.AsrProcessingToken;
        taskQueue.QueueWithDependency<AsrService>(
            queueName: "asr",
            taskName: $"Reset ASR for audio {id}",
            task: svc => svc.ProcessAudioAsrAsync(id, asrProcessingToken));

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
        var filePath = audio.FilePath;
        var pendingFilePath = audio.PendingFilePath;
        context.Audios.Remove(audio);
        fileCleanupService.QueueDeletion(filePath);
        fileCleanupService.QueueDeletion(pendingFilePath);
        await context.SaveChangesAsync();
        await fileCleanupService.TryCleanupQueuedAsync();

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

    private async Task<AudioUpload?> GetConsumableUploadAsync(
        Guid uploadId,
        string ownerId,
        AudioUploadPurpose purpose,
        int? targetAudioId)
    {
        var upload = await context.AudioUploads.FirstOrDefaultAsync(item =>
            item.Id == uploadId &&
            item.OwnerId == ownerId &&
            item.Purpose == purpose &&
            item.TargetAudioId == targetAudioId &&
            item.ConsumedTime == null &&
            item.ExpiresTime > DateTime.UtcNow);
        if (upload == null)
        {
            return null;
        }
        upload.ConsumedTime = DateTime.UtcNow;
        upload.ConcurrencyToken = Guid.NewGuid().ToString("N");
        return upload;
    }

    private void QueueMediaProcessing(int audioId)
    {
        mediaQueueService.QueueIfNotActive(audioId);
    }

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
