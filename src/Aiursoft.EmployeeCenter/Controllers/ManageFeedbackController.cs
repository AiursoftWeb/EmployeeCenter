using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ManageFeedbackViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageFeedback)]
[LimitPerMin]
public class ManageFeedbackController(
    EmployeeCenterDbContext context) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal",
        CascadedLinksIcon = "scale",
        CascadedLinksOrder = 5,
        LinkText = "Manage Feedback",
        LinkOrder = 4)]
    public async Task<IActionResult> Index()
    {
        var questionnaires = await context.SignalQuestionnaires
            .Include(q => q.Questions)
            .Include(q => q.Responses)
            .OrderByDescending(q => q.CreationTime)
            .ToListAsync();

        return this.StackView(new IndexViewModel { Questionnaires = questionnaires });
    }

    public async Task<IActionResult> Questions()
    {
        var questions = await context.SignalQuestions
            .OrderByDescending(q => q.CreationTime)
            .ToListAsync();

        return this.StackView(new QuestionsViewModel { Questions = questions });
    }

    public IActionResult CreateQuestion()
    {
        return this.StackView(new CreateQuestionViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion(CreateQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = new SignalQuestion
            {
                Title = model.Title,
                Type = model.Type,
                Tags = model.Tags,
                Meta = model.Meta
            };
            context.SignalQuestions.Add(question);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Questions));
        }
        return this.StackView(model);
    }

    public async Task<IActionResult> EditQuestion(int id)
    {
        var question = await context.SignalQuestions.FindAsync(id);
        if (question == null) return NotFound();

        return this.StackView(new EditQuestionViewModel
        {
            Id = question.Id,
            Title = question.Title,
            Type = question.Type,
            Tags = question.Tags,
            Meta = question.Meta
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion(EditQuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var question = await context.SignalQuestions.FindAsync(model.Id);
            if (question == null) return NotFound();

            question.Title = model.Title;
            question.Type = model.Type;
            question.Tags = model.Tags;
            question.Meta = model.Meta;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Questions));
        }
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var question = await context.SignalQuestions.FindAsync(id);
        if (question == null) return NotFound();

        context.SignalQuestions.Remove(question);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Questions));
    }

    public async Task<IActionResult> CreateQuestionnaire()
    {
        var allQuestions = await context.SignalQuestions.ToListAsync();
        return this.StackView(new CreateQuestionnaireViewModel
        {
            AllAvailableQuestions = allQuestions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestionnaire(CreateQuestionnaireViewModel model)
    {
        if (ModelState.IsValid)
        {
            var questionnaire = new SignalQuestionnaire
            {
                Title = model.Title,
                Description = model.Description,
                IsActive = model.IsActive
            };
            context.SignalQuestionnaires.Add(questionnaire);
            await context.SaveChangesAsync();

            if (model.SelectedQuestionIds.Any())
            {
                for (int i = 0; i < model.SelectedQuestionIds.Count; i++)
                {
                    context.SignalQuestionnaireQuestions.Add(new SignalQuestionnaireQuestion
                    {
                        QuestionnaireId = questionnaire.Id,
                        QuestionId = model.SelectedQuestionIds[i],
                        Order = i
                    });
                }
                await context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        model.AllAvailableQuestions = await context.SignalQuestions.ToListAsync();
        return this.StackView(model);
    }

    public async Task<IActionResult> EditQuestionnaire(int id)
    {
        var questionnaire = await context.SignalQuestionnaires
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (questionnaire == null) return NotFound();

        var allQuestions = await context.SignalQuestions.ToListAsync();
        return this.StackView(new EditQuestionnaireViewModel
        {
            Id = questionnaire.Id,
            Title = questionnaire.Title,
            Description = questionnaire.Description,
            IsActive = questionnaire.IsActive,
            SelectedQuestionIds = questionnaire.Questions.OrderBy(q => q.Order).Select(q => q.QuestionId).ToList(),
            AllAvailableQuestions = allQuestions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestionnaire(EditQuestionnaireViewModel model)
    {
        if (ModelState.IsValid)
        {
            var questionnaire = await context.SignalQuestionnaires
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == model.Id);
            if (questionnaire == null) return NotFound();

            questionnaire.Title = model.Title;
            questionnaire.Description = model.Description;
            questionnaire.IsActive = model.IsActive;

            // Update questions
            context.SignalQuestionnaireQuestions.RemoveRange(questionnaire.Questions);
            if (model.SelectedQuestionIds.Any())
            {
                for (int i = 0; i < model.SelectedQuestionIds.Count; i++)
                {
                    context.SignalQuestionnaireQuestions.Add(new SignalQuestionnaireQuestion
                    {
                        QuestionnaireId = questionnaire.Id,
                        QuestionId = model.SelectedQuestionIds[i],
                        Order = i
                    });
                }
            }

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        model.AllAvailableQuestions = await context.SignalQuestions.ToListAsync();
        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestionnaire(int id)
    {
        var questionnaire = await context.SignalQuestionnaires.FindAsync(id);
        if (questionnaire == null) return NotFound();

        context.SignalQuestionnaires.Remove(questionnaire);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Responses(int id)
    {
        var questionnaire = await context.SignalQuestionnaires
            .Include(q => q.Responses)
            .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (questionnaire == null) return NotFound();

        return this.StackView(new ResponsesViewModel
        {
            Questionnaire = questionnaire,
            Responses = questionnaire.Responses.OrderByDescending(r => r.SubmitTime).ToList()
        });
    }

    public async Task<IActionResult> ResponseDetail(int id)
    {
        var response = await context.SignalResponses
            .Include(r => r.User)
            .Include(r => r.Questionnaire)
            .Include(r => r.QuestionResponses)
            .ThenInclude(qr => qr.Question)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (response == null) return NotFound();

        return this.StackView(new ResponseDetailViewModel { Response = response });
    }
}
