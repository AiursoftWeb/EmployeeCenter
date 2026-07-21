using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.AudioViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanViewAudio)]
[LimitPerMin]
public class AudioController(
    EmployeeCenterDbContext context,
    AsrService asrService)
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

        var audios = await context.Audios
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
            TotalAudioCount = await context.Audios.CountAsync(),
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
            var audio = new Audio
            {
                Name = model.Name,
                FilePath = model.FilePath!,
                CreateTime = DateTime.UtcNow
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

        var plainText = await asrService.GetAsrResultByAudioIdAsync(id);

        return this.StackView(new TranscriptViewModel
        {
            Audio = audio,
            PlainText = plainText
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

        await asrService.ProcessAudioAsrAsync(id);

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

        var plainText = await asrService.GetAsrResultByAudioIdAsync(id);
        if (string.IsNullOrEmpty(plainText))
        {
            return Content(string.Empty, "text/plain");
        }

        return Content(plainText, "text/plain", System.Text.Encoding.UTF8);
    }
}

