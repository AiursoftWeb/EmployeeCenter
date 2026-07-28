using Aiursoft.Scanner.Abstractions;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Html;

namespace Aiursoft.EmployeeCenter.Services;

public class MarkdownDisplayService(MarkdownPipeline pipeline, HtmlSanitizer sanitizer) : ITransientDependency
{
    public HtmlString RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new HtmlString(string.Empty);
        }

        var html = Markdown.ToHtml(markdown, pipeline);
        html = sanitizer.Sanitize(html);
        return new HtmlString(html);
    }
}
