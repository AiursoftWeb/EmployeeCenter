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

[Authorize(Policy = AppPermissionNames.CanViewAudio)]
[LimitPerMin]
public class AudioController(
    EmployeeCenterDbContext context,
    AsrService asrService,
    ServiceTaskQueue taskQueue,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuthorizationService authorizationService)
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
        var currentUser = await userManager.GetUserAsync(User);
        var userDepartment = currentUser?.Department;
        var userRoleIds = await GetUserRoleIdsAsync();

        IQueryable<Audio> query = context.Audios;
        if (!isManager)
        {
            // 非管理员：仅返回自己可见的录音（本人 / 公开 / 同部门 / 被单独分享）。
            query = query.Where(a =>
                a.OwnerId == userId ||
                a.ViewScope == AudioViewScope.Public ||
                (a.ViewScope == AudioViewScope.Department && a.OwnerDepartment == userDepartment) ||
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
                IsEmptyResult = context.AudioAsrResults.Any(r => r.AudioId == a.Id && r.PlainText == "")
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
            var audio = new Audio
            {
                Name = model.Name,
                FilePath = model.FilePath!,
                CreateTime = DateTime.UtcNow,
                OwnerId = user!.Id,
                OwnerDepartment = user.Department,
                ViewScope = model.ViewScope
            };
            context.Audios.Add(audio);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return this.StackView(model);
    }

    public async Task<IActionResult> Transcript(int id)
    {
        var audio = await context.Audios.FirstOrDefaultAsync(a => a.Id == id);
        if (audio == null) return NotFound();
        if (!await CanViewAudioAsync(audio)) return NotFound();

        var canManageShares = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded
                               || audio.OwnerId == userManager.GetUserId(User);

        var plainText = await asrService.GetAsrResultByAudioIdAsync(id);

        return this.StackView(new TranscriptViewModel
        {
            Audio = audio,
            PlainText = plainText,
            CanManageShares = canManageShares
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageAudio)]
    public async Task<IActionResult> ResetAsr(int id)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();

        var existingResult = await context.AudioAsrResults.FirstOrDefaultAsync(r => r.AudioId == id);
        if (existingResult != null)
        {
            context.AudioAsrResults.Remove(existingResult);
        }

        audio.AsrAttemptCount = 0;
        audio.EmptyResultCount = 0;
        audio.LastAsrAttemptTime = null;
        await context.SaveChangesAsync();

        // 将 ASR 处理放入独立后台任务，避免长耗时（最长 TimeoutSeconds）请求阻塞当前 HTTP 请求。
        taskQueue.QueueWithDependency<AsrService>(
            queueName: "asr",
            taskName: $"Reset ASR for audio {id}",
            task: svc => svc.ProcessAudioAsrAsync(id));

        return RedirectToAction(nameof(Transcript), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageAudio)]
    public async Task<IActionResult> Delete(int id)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio != null)
        {
            var results = await context.AudioAsrResults.Where(r => r.AudioId == id).ToListAsync();
            context.AudioAsrResults.RemoveRange(results);
            context.Audios.Remove(audio);
            await context.SaveChangesAsync();
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetViewScope(int id, AudioViewScope viewScope)
    {
        var audio = await context.Audios.FindAsync(id);
        if (audio == null) return NotFound();
        if (!await CanManageAudioAsync(audio)) return Unauthorized();

        audio.ViewScope = viewScope;
        if (viewScope == AudioViewScope.Department)
        {
            var user = await userManager.GetUserAsync(User);
            audio.OwnerDepartment = user?.Department;
        }

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Transcript), new { id });
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

        if (string.IsNullOrWhiteSpace(model.TargetUserId) && string.IsNullOrWhiteSpace(model.TargetRoleId))
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

    private async Task<bool> CanViewAudioAsync(Audio audio)
    {
        if ((await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanManageAudio)).Succeeded)
        {
            return true;
        }

        var userId = userManager.GetUserId(User);
        if (audio.OwnerId == userId) return true;
        if (audio.ViewScope == AudioViewScope.Public) return true;

        if (audio.ViewScope == AudioViewScope.Department)
        {
            var user = await userManager.GetUserAsync(User);
            if (user?.Department != null && user.Department == audio.OwnerDepartment)
            {
                return true;
            }
        }

        var userRoleIds = await GetUserRoleIdsAsync();
        var shared = await context.AudioShares
            .AnyAsync(s => s.AudioId == audio.Id &&
                           (s.SharedWithUserId == userId ||
                            (s.SharedWithRoleId != null && userRoleIds.Contains(s.SharedWithRoleId))));
        return shared;
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
