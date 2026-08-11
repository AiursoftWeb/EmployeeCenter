using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class AudioUploadsController(
    EmployeeCenterDbContext context,
    StorageService storage,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuthorizationService authorizationService,
    AudioFileCleanupService fileCleanupService) : ControllerBase
{
    private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac", ".webm", ".amr",
        ".mp4", ".mov", ".mkv", ".avi"
    };

    [HttpPost]
    [Route("audio-uploads")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(
        [FromQuery] string purpose,
        [FromQuery] int? targetAudioId,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AudioUploadPurpose>(purpose, ignoreCase: true, out var uploadPurpose))
        {
            return BadRequest("The upload purpose is invalid.");
        }
        if (!await CanUploadAsync(uploadPurpose, targetAudioId))
        {
            return Forbid();
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.FirstOrDefault();
        if (file == null || file.Length == 0 || file.Length > MaxUploadBytes)
        {
            return BadRequest("A non-empty audio or video file within the upload limit is required.");
        }
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("The selected file type is not supported.");
        }

        var ownerId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(ownerId))
        {
            return Unauthorized();
        }
        var uploadId = Guid.NewGuid();
        var logicalPath = $"audio/{ownerId}/{uploadId:N}{extension.ToLowerInvariant()}";
        var savedPath = await storage.Save(logicalPath, file, isVault: true, cancellationToken);
        context.AudioUploads.Add(new AudioUpload
        {
            Id = uploadId,
            OwnerId = ownerId,
            FilePath = savedPath,
            Purpose = uploadPurpose,
            TargetAudioId = targetAudioId,
            ExpiresTime = DateTime.UtcNow.AddHours(24)
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileCleanupService.DeleteIfUnreferencedAsync(savedPath);
            throw;
        }

        return Ok(new
        {
            Path = uploadId,
            InternetPath = storage.RelativePathToInternetUrl(savedPath, HttpContext, isVault: true)
        });
    }

    private async Task<bool> CanUploadAsync(AudioUploadPurpose purpose, int? targetAudioId)
    {
        if (purpose == AudioUploadPurpose.Create)
        {
            return (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded;
        }
        if (targetAudioId == null)
        {
            return false;
        }

        var audio = await context.Audios.FindAsync(targetAudioId.Value);
        if (audio == null)
        {
            return false;
        }
        if ((await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded)
        {
            return true;
        }

        var userId = userManager.GetUserId(User);
        if (audio.OwnerId == userId)
        {
            return true;
        }
        var user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return false;
        }
        var roleNames = await userManager.GetRolesAsync(user);
        var roleIds = await roleManager.Roles
            .Where(role => roleNames.Contains(role.Name!))
            .Select(role => role.Id)
            .ToListAsync();
        return await context.AudioShares.AnyAsync(share =>
            share.AudioId == audio.Id &&
            share.Permission == SharePermission.Editable &&
            (share.SharedWithUserId == userId ||
             (share.SharedWithRoleId != null && roleIds.Contains(share.SharedWithRoleId))));
    }
}
