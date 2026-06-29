using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Adapters;

/// <summary>
/// 明日方舟标签渲染器。
/// 包装 TagProcessor + PlotRules，将方舟标签（[name=][background]等）转换为 Markdown 文本。
/// </summary>
public class ArknightsTagRenderer : ITagRenderer
{
    private readonly TagProcessor _processor;
    private readonly PlotRules _rules;

    public bool RequiresRulesFile => true;

    public ArknightsTagRenderer() : this(new PlotRules(), new NotificationBlock()) { }

    public ArknightsTagRenderer(PlotRules rules, NotificationBlock notify)
    {
        _rules = rules;
        _processor = new TagProcessor(rules, notify);
    }

    public void LoadRules(string rulesFilePath)
    {
        _rules.GetRegsFromJson(rulesFilePath);
    }

    public string RenderLine(ScriptLine line)
    {
        if (line is not FormattedTextEntry entry)
            return line.OriginalText;

        if (string.IsNullOrWhiteSpace(entry.OriginalText))
            return "";

        return _processor.ProcessEntry(entry);
    }
}
