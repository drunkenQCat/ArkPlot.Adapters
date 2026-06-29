using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights;

/// <summary>
/// 明日方舟脚本解析器。
/// 包装 PrtsPreloader，将原始文本中的 [name=][charslot][background] 等标签解析为 ScriptLine。
/// </summary>
public class ArknightsScriptParser : IScriptParser
{
    public string GameId => "arknights";

    public List<ScriptLine> Parse(string rawText, string chapterTitle)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new List<ScriptLine>();

        var lines = SplitIntoEntries(rawText);
        var preloader = CreatePreloader(lines, chapterTitle);
        preloader.ParseAndCollectAssets();

        return lines.Cast<ScriptLine>().ToList();
    }

    public HashSet<ResourceRef> CollectResources(List<ScriptLine> lines)
    {
        var resources = new HashSet<ResourceRef>();

        foreach (var line in lines)
        {
            CollectLineResources(line, resources);
        }

        return resources;
    }

    private static List<FormattedTextEntry> SplitIntoEntries(string rawText)
    {
        var rawLines = rawText.Split('\n', StringSplitOptions.None);
        var entries = new List<FormattedTextEntry>(rawLines.Length);

        for (int i = 0; i < rawLines.Length; i++)
        {
            entries.Add(new FormattedTextEntry
            {
                Index = i,
                OriginalText = rawLines[i].TrimEnd('\r')
            });
        }

        return entries;
    }

    private static PrtsPreloader CreatePreloader(List<FormattedTextEntry> lines, string chapterTitle)
    {
        var plot = new Plot { Title = chapterTitle, TextVariants = lines };
        var plotManager = new PlotManager(plot);
        return new PrtsPreloader(plotManager);
    }

    private static void CollectLineResources(ScriptLine line, HashSet<ResourceRef> resources)
    {
        if (string.IsNullOrWhiteSpace(line.Type))
            return;

        foreach (var url in line.ResourceUrls)
        {
            var resourceType = InferResourceType(line.Type, url);
            resources.Add(new ResourceRef(resourceType, url));
        }
    }

    private static string InferResourceType(string lineType, string url)
    {
        if (lineType is "character" or "charslot" or "charactercutin")
            return "portrait";
        if (lineType == "background")
            return "background";
        if (url.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            return "audio";
        return "image";
    }
}
