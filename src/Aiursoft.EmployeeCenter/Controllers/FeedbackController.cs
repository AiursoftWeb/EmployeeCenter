using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.FeedbackViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanSubmitFeedback)]
[LimitPerMin]
public class FeedbackController(
    EmployeeCenterDbContext context,
    UserManager<User> userManager) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Development",
        CascadedLinksIcon = "git-branch",
        CascadedLinksOrder = 2,
        LinkText = "Employee Signals",
        LinkOrder = 4)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Get active questionnaires that the user hasn't submitted yet
        var submittedQuestionnaireIds = await context.SignalResponses
            .Where(r => r.UserId == user.Id)
            .Select(r => r.QuestionnaireId)
            .ToListAsync();

        var activeQuestionnaires = await context.SignalQuestionnaires
            .Where(q => q.IsActive && !submittedQuestionnaireIds.Contains(q.Id))
            .OrderByDescending(q => q.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel { ActiveQuestionnaires = activeQuestionnaires });
    }

    public async Task<IActionResult> Fill(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var questionnaire = await context.SignalQuestionnaires
            .Include(q => q.Questions)
            .ThenInclude(qq => qq.Question)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (questionnaire == null || !questionnaire.IsActive) return NotFound();

        // Check if already submitted
        var existing = await context.SignalResponses
            .AnyAsync(r => r.QuestionnaireId == id && r.UserId == user.Id);
        if (existing)
        {
            return RedirectToAction(nameof(Index));
        }

        return this.StackView(new FillViewModel
        {
            Questionnaire = questionnaire,
            QuestionnaireId = questionnaire.Id
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fill(FillViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var questionnaire = await context.SignalQuestionnaires
            .Include(q => q.Questions)
            .ThenInclude(qq => qq.Question)
            .FirstOrDefaultAsync(q => q.Id == model.QuestionnaireId);

        if (questionnaire == null || !questionnaire.IsActive) return NotFound();

        // Check if already submitted
        var existing = await context.SignalResponses
            .AnyAsync(r => r.QuestionnaireId == model.QuestionnaireId && r.UserId == user.Id);
        if (existing)
        {
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            var response = new SignalResponse
            {
                QuestionnaireId = model.QuestionnaireId,
                UserId = user.Id,
                SubmitTime = DateTime.UtcNow
            };
            context.SignalResponses.Add(response);
            await context.SaveChangesAsync();

            foreach (var answer in model.Answers)
            {
                context.SignalQuestionResponses.Add(new SignalQuestionResponse
                {
                    SignalResponseId = response.Id,
                    QuestionId = answer.Key,
                    Answer = answer.Value
                });
            }
            await context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        model.Questionnaire = questionnaire;
        return this.StackView(model);
    }
}
