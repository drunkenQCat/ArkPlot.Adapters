using System.IO;
using ArkPlot.Core.Model;
using Markdig;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Workflow;

public abstract partial class AkpProcessor
{
    public static void WriteMd(string path, Plot markdown)
    {
        var mdOutPath = Path.Combine(path, markdown.Title + ".md");
        File.WriteAllText(mdOutPath, markdown.Content.ToString());
    }

    public static void WriteHtml(string path, Plot markdown)
    {
        var htmlPath = Path.Combine(path, markdown.Title + ".html");
        var htmlContent = GetHtmlContent(markdown);
        var result = FormatHtmlBody(htmlContent, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    public static void WriteHtmlWithLocalRes(string path, Plot markdown)
    {
        var htmlPath = Path.Combine(path, markdown.Title + ".html");
        var htmlContent = GetHtmlContent(markdown);
        var htmlWithLocalRes = htmlContent.Replace("https://", "");
        var result = FormatHtmlBody(htmlWithLocalRes, markdown.Title);
        File.WriteAllText(htmlPath, result);
    }

    private static string GetHtmlContent(Plot markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(markdown.Content.ToString(), pipeline);
    }

    private static string FormatHtmlBody(string body, string title)
    {
        body = $"<body>{body}</body>";
        title = $"<title>{title}</title>";
        var head = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets/head.html"));
        head = $"<head>{head}{title}</head>";
        var html = $"<!doctype html><html>{head}{body}</html>";
        var tail = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets/tail.html"));
        return html + tail;
    }
}
