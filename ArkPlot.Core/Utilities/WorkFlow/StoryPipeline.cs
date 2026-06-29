using System.Text;
using System.Threading;
using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

namespace ArkPlot.Core.Utilities.WorkFlow;

/// <summary>
/// 通用剧情处理管线。
/// 串联数据获取 → 脚本解析 → 标签渲染 → 图片描述 → 文档构建。
/// 不依赖任何特定游戏逻辑，所有游戏特性通过接口注入。
/// </summary>
public class StoryPipeline
{
    private readonly IStoryDataProvider _provider;
    private readonly IScriptParser _parser;
    private readonly ITagRenderer _renderer;
    private readonly PicDescService _picDescService;

    public StoryPipeline(
        IStoryDataProvider provider,
        IScriptParser parser,
        ITagRenderer renderer,
        PicDescService picDescService)
    {
        _provider = provider;
        _parser = parser;
        _renderer = renderer;
        _picDescService = picDescService;
    }

    /// <summary>
    /// 处理单个活动：下载所有章节 → 解析 → 渲染 → 输出 Markdown。
    /// </summary>
    public async Task<List<(string Title, string Markdown)>> ProcessEventAsync(
        Act act,
        List<StoryChapter> chapters,
        OutputMode outputMode = OutputMode.Readable,
        CancellationToken ct = default)
    {
        var results = new List<(string Title, string Markdown)>();

        foreach (var chapter in chapters)
        {
            ct.ThrowIfCancellationRequested();

            var lines = await ProcessChapterAsync(chapter, act.Lang, ct);
            var markdown = BuildDocument(lines, outputMode);
            results.Add((chapter.StoryName, markdown));
        }

        return results;
    }

    /// <summary>
    /// 处理单个章节：获取原始文本 → 解析 → 渲染 → 图片描述。
    /// </summary>
    public async Task<List<ScriptLine>> ProcessChapterAsync(
        StoryChapter chapter,
        string lang,
        CancellationToken ct = default)
    {
        var rawText = await _provider.FetchChapterAsync(chapter, ct);
        if (string.IsNullOrWhiteSpace(rawText))
            return new List<ScriptLine>();

        var lines = _parser.Parse(rawText, chapter.StoryName);
        RenderLines(lines);
        await EnrichDescriptionsAsync(lines, ct);

        return lines;
    }

    /// <summary>将 ScriptLine 列表转换为 Markdown 文档。</summary>
    public static string BuildDocument(List<ScriptLine> lines, OutputMode mode)
    {
        // 将 ScriptLine 转为 FormattedTextEntry 以兼容现有 StoryDocumentBuilder
        var entries = lines.OfType<FormattedTextEntry>().ToList();
        if (entries.Count == 0)
            return string.Empty;

        var builder = new StoryDocumentBuilder(entries, outputMode: mode);
        return builder.Result;
    }

    private void RenderLines(List<ScriptLine> lines)
    {
        foreach (var line in lines)
        {
            line.MdText = _renderer.RenderLine(line);
        }
    }

    private async Task EnrichDescriptionsAsync(List<ScriptLine> lines, CancellationToken ct)
    {
        var entries = lines.OfType<FormattedTextEntry>().ToList();
        if (entries.Count == 0) return;

        await PicDescEnricher.EnrichAsync(entries, _picDescService, ct);
    }
}
