using ArkPlot.Core.Model;
using CharacterChart = System.Collections.Generic.Dictionary<string, string>;
using EntryList = System.Collections.Generic.List<ArkPlot.Core.Model.ScriptLine>;

namespace ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

/// <summary>
/// 立绘处理管线：角色识别、图表生成、描述注入、立绘行清理。
/// </summary>
public static class PortraitProcessor
{
    public static void DetectPortraits(StoryDocumentContext ctx)
    {
        foreach (var group in ctx.Groups)
        {
            if (group.Any(e => IsPortrait(e)))
                AppendPortrait(ctx, group);
        }
    }

    public static void GenerateCharts(StoryDocumentContext ctx)
    {
        foreach (var portraitGrp in ctx.Portraits)
        {
            var chart = GenerateChartDict(portraitGrp.PortraitMarks);
            MakePortraitChart(ctx, portraitGrp, chart);
        }
    }

    /// <summary>
    /// Prompt 模式：将 portrait-facts 内联到角色首次出现的位置（第一个非立绘行之前）。
    /// 确保只出现但不说话的角色也能获得描述，且每个角色每章节只出现一次。
    ///
    /// IMPORTANT（Debug 提示）：
    /// 本方法必须在 RemovePortraitLines / GenerateCharts 之前、PromptRenderer 渲染之前执行。
    /// 原因：RemovePortraitLines 会清空立绘行的 MdText，而 PromptRenderer 会跳过 MdText 为空的行，
    /// 且 EmitCharacterFacts 只在"对话行"上触发。因此如果"只出场不说话"的角色没有在本方法里
    /// 提前注入 facts，它就会在整条管线里彻底丢失描述（立绘行被清空、无对话行可触发）。
    /// 本方法靠 DescribedCharacters 去重，保证每个角色每章节只注入一次。
    /// </summary>
    public static void InjectPortraitFactsForPrompt(StoryDocumentContext ctx)
    {
        foreach (var portraitGrp in ctx.Portraits)
        {
            // 注入点：该立绘组内第一个"可被渲染输出"的行（对话/旁白/场景等）。
            // 不能选 playmusic/停音乐等会被 PromptRenderer 跳过的行，否则注入的描述会被一起丢弃。
            var targetLine = portraitGrp.SList.FirstOrDefault(line => !IsPortrait(line) && IsRenderable(line));
            if (targetLine is null) continue;

            var factsBlocks = new List<string>();
            foreach (var mark in portraitGrp.PortraitMarks)
            {
                var entry = mark.OriginalEntry;
                var code = entry.CharacterCode;
                // 无 CharacterCode 跳过。
                if (string.IsNullOrEmpty(code))
                    continue;

                // 去重键：角色 code + 出场方式（bstart 隐藏 / 常规露出）。
                // 同一角色在"隐藏出场"（带 bstart，遮脸）与"露出真容"（无 bstart）各触发一次描述。
                // 用 bstart 区分，保证 aphris 等角色隐藏期和露出期都有描述。
                var hasBstart = entry.CommandSet.TryGetValue("bstart", out var bs) && !string.IsNullOrEmpty(bs);
                var dedupKey = hasBstart ? $"{code}:bstart" : $"{code}:open";
                if (ctx.DescribedCharacters.Contains(dedupKey))
                    continue;

                var facts = GetOrGenerateFacts(mark);
                if (string.IsNullOrEmpty(facts))
                    continue;

                ctx.DescribedCharacters.Add(dedupKey);
                factsBlocks.Add(
                    $"<aside class=\"portrait-facts\" data-character=\"{mark.Name}\">\n{facts}\n</aside>");
            }

            if (factsBlocks.Count == 0) continue;

            // 把该组所有角色的 facts 拼成一个块，插到注入行内容的最前面。
            var prefix = string.Join("\r\n\r\n", factsBlocks) + "\r\n\r\n";
            targetLine.MdText = prefix + targetLine.MdText;
        }
    }

    /// <summary>
    /// 判断某行是否会被 PromptRenderer 实际渲染输出（非音乐/非空行）。
    /// 注入立绘描述时只能选这种行，否则描述会被渲染器跳过。
    /// </summary>
    private static bool IsRenderable(ScriptLine line)
    {
        if (PromptRendererConfig.Arknights.MusicSkipTypes.Contains(line.Type))
            return false;
        if (string.IsNullOrWhiteSpace(line.MdText) && string.IsNullOrWhiteSpace(line.PicDesc))
            return false;
        return true;
    }

    public static void Process(StoryDocumentContext ctx)
    {
        DetectPortraits(ctx);
        GenerateCharts(ctx);
    }

    /// <summary>
    /// 清空所有立绘行的 MdText。
    ///
    /// IMPORTANT（Debug 提示）：
    /// 1. 立绘行（Type 含 "char"）本身只是"角色出场"的指令，不含正文，清空后由后续对话/旁白承载内容。
    /// 2. 清空后 PromptRenderer 会跳过这些行（MdText 为空），ReadableRenderer 则用立绘表格替代显示。
    /// 3. 本方法必须在 InjectPortraitFactsForPrompt 之后执行——否则 Prompt 模式下
    ///    "只出场不说话"的角色的 facts 会被一起清掉，导致描述丢失。
    /// 4. 若发现某角色描述缺失，优先检查：本方法是否在 InjectPortraitFactsForPrompt 之前被调用；
    ///    或该角色的立绘行是否被误判为无 CharacterCode / 无 facts。
    /// </summary>
    public static void RemovePortraitLines(StoryDocumentContext ctx)
    {
        foreach (var digit in ctx.PortraitIndexes)
            ctx.Lines[digit].MdText = "";
    }

    private static string GetOrGenerateFacts(CharacterInfo mark)
    {
        var facts = mark.OriginalEntry.PicFacts;
        if (!string.IsNullOrWhiteSpace(facts))
            return facts;

        var desc = mark.OriginalEntry.PicDesc;
        if (string.IsNullOrWhiteSpace(desc)
            || desc.StartsWith("[PIC_DESC:")
            || desc.StartsWith("[DESC_ERROR:"))
            return "";

        return desc;
    }

    private static void AppendPortrait(StoryDocumentContext ctx, EntryList grp)
    {
        var characters = ExtractCharacterInfo(ctx, grp);
        ctx.Portraits.Add(new PortraitGrp(grp, characters));
    }

    private static List<CharacterInfo> ExtractCharacterInfo(StoryDocumentContext ctx, EntryList paragraphLines)
    {
        // 预构建 立绘行 CharacterCode → 角色名 映射（单章节内一般唯一，见 Debug 说明）。
        // 用于把立绘行关联到其真正所属的角色，替代脆弱的 line.Index + 1 邻近行推断。
        var codeToName = BuildCodeToNameMap(ctx.Lines);

        var characters = new List<CharacterInfo>();
        foreach (var line in paragraphLines)
        {
            if (!IsPortrait(line))
                continue;
            ctx.PortraitIndexes.Add(line.Index);
            var characterName = ExtractCharacterNameFromLines(ctx.Lines, line, codeToName);
            var url = line.MdText.Split("\r\n")[0];
            characters.Add(
                !string.IsNullOrWhiteSpace(characterName)
                    ? new CharacterInfo(line, characterName, url)
                    : new CharacterInfo(line, "Unknown", string.Empty));
        }
        return characters;
    }

    /// <summary>
    /// 扫描整章对话行，把每个立绘行的 CharacterCode 关联到其对应的角色名。
    ///
    /// 来源差异：立绘行 code 来自 charslot 的 name 参数（如 "avg_486_espumo_1"，"avg_" 是角色编号前缀）；
    /// 对话行 code 来自 Propagate 对立绘焦点 URL 文件名的提取（如 "Avg_avg_486_espumo_1"/"Avg_char_130_doberm_ex_2"，
    /// 比立绘行 code 多了 "Avg_" 前缀与表情序号 "_N"/"$N" 后缀）。因此这里先收集所有立绘行 code，
    /// 再对每个对话行 code 做"去前缀、前缀匹配"的关联，避免误删角色编号自身的数字尾缀。
    ///
    /// Debug 说明：单章节内某个立绘 code 通常唯一对应一个角色（除「？？？」等神秘人物，
    /// 或同一 NPC 立绘被多个无名人复用）。若未来遇到此类复用，需在此处引入更细的消歧规则。
    /// </summary>
    private static Dictionary<string, string> BuildCodeToNameMap(EntryList lineList)
    {
        // 第一步：收集所有立绘行（char*/charslot/charactercutin）的原始 CharacterCode。
        var portraitCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in lineList)
        {
            if (IsPortrait(entry) && !string.IsNullOrEmpty(entry.CharacterCode))
                portraitCodes.Add(entry.CharacterCode);
        }

        // 第二步：扫描对话行，把对话行 code（去 Avg_ 前缀）与立绘行 code 做前缀匹配。
        // 立绘行 code 是对话行 code 的子串前缀（去掉表情/序号后缀后）。
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in lineList)
        {
            if (string.IsNullOrEmpty(entry.CharacterName)
                || string.IsNullOrEmpty(entry.CharacterCode))
                continue;
            var trimmed = StripAvgPrefix(entry.CharacterCode);
            foreach (var pc in portraitCodes)
            {
                // 对话行 code 以立绘行 code 为前缀（如 "Avg_char_130_doberm_ex_2" 以 "char_130_doberm_ex" 开头）
                if (trimmed.StartsWith(pc, StringComparison.OrdinalIgnoreCase)
                    && !map.ContainsKey(pc))
                {
                    map[pc] = entry.CharacterName;
                    break;
                }
            }
        }
        return map;
    }

    /// <summary>去掉对话行 code 开头的 "Avg_" 图片资源前缀（区分大小写，保留 "avg_" 角色前缀）。</summary>
    private static string StripAvgPrefix(string code)
        => code.StartsWith("Avg_", StringComparison.Ordinal) ? code[4..] : code;

    private static string ExtractCharacterNameFromLines(
        EntryList lineList,
        ScriptLine line,
        Dictionary<string, string> codeToName)
    {
        // 优先用立绘行自身的 CharacterCode 关联到对应角色名（最可靠）。
        if (!string.IsNullOrEmpty(line.CharacterCode)
            && codeToName.TryGetValue(line.CharacterCode, out var name))
        {
            return name;
        }

        // 回退：取紧邻下一行的角色名（旧行为，兼容无 CharacterCode 的立绘行）。
        var nameEntry = lineList.ElementAtOrDefault(line.Index + 1);
        return nameEntry is not null && !string.IsNullOrEmpty(nameEntry.CharacterName)
            ? nameEntry.CharacterName
            : "";
    }

    public static bool IsPortrait(ScriptLine item)
    {
        return item.Type.Contains("Char") || item.Type.Contains("char");
    }

    private static CharacterChart GenerateChartDict(List<CharacterInfo> characters)
    {
        var charaDict = new CharacterChart();
        foreach (var character in characters)
        {
            if (character.Name == "Unknown") continue;
            charaDict[character.Name] = EmbedTitleInPortraitHtml(character);
        }
        return charaDict;
    }

    private static string EmbedTitleInPortraitHtml(CharacterInfo character)
    {
        var htmlTag = new HtmlTagParser(character.PortraitHtml)
        {
            Attributes = { ["title"] = character.Name },
        };
        var portraitUrl = GetPortraitUrl(character.OriginalEntry);
        if (!string.IsNullOrEmpty(portraitUrl))
            htmlTag.Attributes["src"] = portraitUrl;
        return $"<div class=\"crop\">{htmlTag.ReconstructHtml()}</div>";
    }

    private static string GetPortraitUrl(ScriptLine entry)
    {
        if (entry.ResourceUrls.Count == 0)
            return "";

        _ = entry.CommandSet.TryGetValue("focus", out string? focusIndex);
        if (int.TryParse(focusIndex, out var focusIdx))
        {
            if (focusIdx > 0 && focusIdx <= entry.ResourceUrls.Count)
                return entry.ResourceUrls[focusIdx - 1];
        }

        return entry.ResourceUrls[0];
    }

    private static void MakePortraitChart(
        StoryDocumentContext ctx,
        PortraitGrp group,
        CharacterChart portraitLinks)
    {
        if (portraitLinks.Count == 0) return;

        var chartItems = string.Join("|", portraitLinks.Values);
        var chartHead = $"|{chartItems}|";
        var chartSeg = $"|{string.Concat(Enumerable.Repeat(" --- |", portraitLinks.Count))}";

        var chartBody = ctx.EnableDescriptions
            ? BuildDescribedChart(ctx, group, portraitLinks)
            : $"{chartHead}\r\n{chartSeg}\r\n\r\n";

        var firstLine = group.SList.First();
        firstLine.MdText = firstLine.MdText.Insert(0, chartBody);
    }

    private static string BuildDescribedChart(
        StoryDocumentContext ctx,
        PortraitGrp group,
        CharacterChart portraitLinks)
    {
        var headCells = string.Join("\r\n", portraitLinks.Values.Select(v => $"    <td>{v}</td>"));
        var descCells = new List<string>();
        foreach (var charName in portraitLinks.Keys)
        {
            var mark = group.PortraitMarks.FirstOrDefault(m => m.Name == charName);
            var desc = "";
            if (mark != null)
            {
                var code = mark.OriginalEntry.CharacterCode;
                if (!string.IsNullOrEmpty(code) && !ctx.DescribedCharacters.Contains(code))
                {
                    ctx.DescribedCharacters.Add(code);
                    desc = GetOrGenerateDescription(mark);
                }
            }
            var cellText = string.IsNullOrEmpty(desc)
                ? charName
                : $"【此处为对{charName}的形象描述，请结合上下文将其融入文中，不要生搬硬套】：{desc}";
            descCells.Add($"    <td>{cellText.Replace("\r\n", "<br>").Replace("\n", "<br>")}</td>");
        }

        return $"<table class=\"portrait-table\">\r\n"
            + $"<tr>\r\n{string.Join("\r\n", headCells)}\r\n</tr>\r\n"
            + $"<tr>\r\n{string.Join("\r\n", descCells)}\r\n</tr>\r\n"
            + $"</table>\r\n\r\n";
    }

    private static string GetOrGenerateDescription(CharacterInfo mark)
    {
        var desc = mark.OriginalEntry.PicDesc;
        if (string.IsNullOrWhiteSpace(desc)
            || desc.StartsWith("[PIC_DESC:")
            || desc.StartsWith("[DESC_ERROR:"))
            return "";
        return desc;
    }
}