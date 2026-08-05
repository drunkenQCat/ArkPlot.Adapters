using ArkPlot.Core.Model;
using EntryList = System.Collections.Generic.List<ArkPlot.Core.Model.ScriptLine>;

namespace ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

/// <summary>
/// StoryDocument 构建器：Entry → 分段 → 立绘处理 → 渲染。
/// 纯同步，不含外部服务调用。描述数据应在构造前通过 PicDescEnricher 预填充。
/// </summary>
public class StoryDocumentBuilder
{
    private readonly StoryDocumentContext _ctx;
    private readonly IMdRenderer _renderer;

    public StoryDocumentBuilder(
        EntryList entries,
        bool enableDescriptions = true,
        OutputMode outputMode = OutputMode.Readable
    )
    {
        _ctx = new StoryDocumentContext(new EntryList(entries), enableDescriptions);
        _renderer = outputMode == OutputMode.PromptOptimized
            ? new PromptRenderer(_ctx.DescribedCharacters)
            : new ReadableRenderer(enableDescriptions, _ctx.DescribedImages);

        RemoveEmptyLines();
        NormalizeStageDirections();
        _ctx.Groups.AddRange(SegmentGrouper.Group(_ctx.Lines));

        // ── 立绘处理管线。顺序有严格依赖，改动需谨慎（Debug 提示）──
        // 1. DetectPortraits：识别所有立绘组，收集 PortraitIndexes 与 DescribedCharacters 的初始状态。
        // 2. InjectPortraitFactsForPrompt（仅 Prompt）：必须在此执行，把 portrait-facts 内联到
        //    每个立绘组第一个非立绘行之前。若挪到 RemovePortraitLines 之后，立绘行已被清空，
        //    "只出场不说话"的角色会因无后续对话行可触发 EmitCharacterFacts 而丢失描述。
        // 3. RemovePortraitLines：清空立绘行 MdText（PromptRenderer 会跳过空行）。
        // 4. GenerateCharts（仅 Readable）：生成 HTML 立绘表格。必须放在 RemovePortraitLines 之后，
        //    否则表格被插入尚未清空的立绘行，随后又被 RemovePortraitLines 一并清掉。
        PortraitProcessor.DetectPortraits(_ctx);
        if (outputMode == OutputMode.PromptOptimized)
            PortraitProcessor.InjectPortraitFactsForPrompt(_ctx);
        PortraitProcessor.RemovePortraitLines(_ctx);
        if (outputMode != OutputMode.PromptOptimized)
            PortraitProcessor.GenerateCharts(_ctx);
    }

    private void RemoveEmptyLines()
    {
        var filtered = _ctx.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.MdText))
            .ToList();
        _ctx.Lines.Clear();
        _ctx.Lines.AddRange(filtered);
        int idx = 0;
        _ctx.Lines.ForEach(line => { line.Index = idx; idx++; });
    }

    /// <summary>
    /// 统一归一化游戏引擎指令，使其更适合 LLM 小说化输入。
    /// 影响 Readable 和 Prompt 两种输出模式。
    /// </summary>
    private void NormalizeStageDirections()
    {
        foreach (var line in _ctx.Lines)
        {
            if (string.IsNullOrEmpty(line.MdText)) continue;
            line.MdText = line.MdText
                .Replace("`瞳孔地震`", "`震惊`")
                .Replace("`图像平移`", "`场景流转`");
        }
    }

    public string Result
    {
        get
        {
            var sb = new StringBuilder();
            AppendResultToBuilder(sb);
            return sb.ToString();
        }
    }

    public void AppendResultToBuilder(StringBuilder builder)
    {
        builder.AppendLine();
        var hasContent = false;
        var sep = _renderer.GroupSeparator;
        foreach (var group in _ctx.Groups)
        {
            var lines = _renderer.Render(group);
            if (lines.Count == 0) continue;
            if (hasContent) builder.Append(sep);
            builder.AppendJoin("\r\n\r\n", lines);
            hasContent = true;
        }
        builder.AppendLine();
    }
}