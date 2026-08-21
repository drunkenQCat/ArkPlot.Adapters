using System.IO;
using ArkPlot.Core.Model;
using EntryList = System.Collections.Generic.List<ArkPlot.Core.Model.ScriptLine>;

namespace ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

/// <summary>
/// Prompt 模式渲染器：aside + YAML 事实，面向 LLM 输入优化。
/// </summary>
internal class PromptRenderer : IMdRenderer
{
    private readonly HashSet<string> _describedCharacters;
    private readonly PromptRendererConfig _config;

    /// <summary>CharacterCode → 该角色在本组内出现过的所有名字（按出现顺序去重）。</summary>
    private readonly Dictionary<string, List<string>> _codeToNames = new(StringComparer.OrdinalIgnoreCase);

    public string GroupSeparator => "\r\n\r\n---\r\n\r\n";

    public PromptRenderer(HashSet<string> describedCharacters, PromptRendererConfig? config = null)
    {
        _describedCharacters = describedCharacters;
        _config = config ?? PromptRendererConfig.Arknights;
    }

    public List<string> Render(EntryList grp)
    {
        CollectCodeToNames(grp);

        var mdList = new List<string>();
        foreach (var entry in grp)
        {
            if (_config.MusicSkipTypes.Contains(entry.Type))
                continue;

            if (string.IsNullOrWhiteSpace(entry.MdText) && string.IsNullOrWhiteSpace(entry.PicDesc))
                continue;

            if (entry.MdText.Trim() == "---")
                continue;

            var mdText = NormalizeText(entry);
            mdText = NormalizeOptionLine(mdText);
            mdText = AnnotateCrossName(entry, mdText);

            EmitCharacterFacts(entry, mdList);
            mdList.Add(mdText);
            EmitSceneFacts(entry, mdList);
            EmitItemFacts(entry, mdList);
        }
        return mdList;
    }

    /// <summary>
    /// 预扫描整组，构建 CharacterCode → 名字序列 映射。
    /// 同一 code 在章节内可能换名（如「精英打扮的男性」→「小贾斯汀」），LLM 需据此识别为同一人。
    /// </summary>
    private void CollectCodeToNames(EntryList grp)
    {
        _codeToNames.Clear();
        foreach (var entry in grp)
        {
            if (string.IsNullOrEmpty(entry.CharacterCode) || string.IsNullOrEmpty(entry.CharacterName))
                continue;

            if (!_codeToNames.TryGetValue(entry.CharacterCode, out var names))
            {
                names = new List<string>();
                _codeToNames[entry.CharacterCode] = names;
            }

            if (!names.Contains(entry.CharacterName, StringComparer.Ordinal))
                names.Add(entry.CharacterName);
        }
    }

    /// <summary>
    /// 对话行跨名提示：同一 CharacterCode 在本组内换名时，标注当前名的前后关系，
    /// 让 LLM 明确前后是同一人。仅对携带 CharacterCode 且名字发生过变化的角色生效。
    /// </summary>
    private string AnnotateCrossName(ScriptLine entry, string mdText)
    {
        if (string.IsNullOrEmpty(entry.CharacterCode) || string.IsNullOrEmpty(entry.CharacterName))
            return mdText;

        if (!_codeToNames.TryGetValue(entry.CharacterCode, out var names) || names.Count <= 1)
            return mdText;

        var firstName = names[0];
        var lastName = names[^1];
        var marker = string.Equals(entry.CharacterName, firstName, StringComparison.Ordinal)
            ? $"（后文作“{StripRevealSuffix(lastName)}”）"
            : $"（即前文“{StripRevealSuffix(firstName)}”）";

        return mdText.Replace($"**{entry.CharacterName}**", $"**{entry.CharacterName}{marker}**");
    }

    /// <summary>
    /// 去掉 NormalizeDisplayNames 附加的揭示名后缀「（实际人物：X）」。
    /// 跨名标注引用「前文名/后文名」时必须显示原始名字，
    /// 否则「即前文'神秘人士（实际人物：可萝尔）'」会与前文名里的揭示说明形成嵌套冗余。
    /// 注意：仅作用于标注内的引用文本；entry.CharacterName 自身与 aside 的 data-character 保持不变。
    /// </summary>
    private static string StripRevealSuffix(string name)
    {
        const string revealMarker = "（实际人物：";
        var idx = name.IndexOf(revealMarker, StringComparison.Ordinal);
        if (idx <= 0 || !name.EndsWith("）", StringComparison.Ordinal))
            return name;
        return name[..idx];
    }

    /// <summary>
    /// 识别 `选项` 行并转换为带博士(玩家)归属的形式，消除 LLM 输入里选项无人认领的问题。
    ///
    /// 原始脚本中选项由一对标签构成：
    ///   - decision：`选项候选1;候选2`  —— 博士可说的候选台词（分号分隔）
    ///   - predicate：`选项1;2;3`      —— 分支编号行（纯数字，不含台词）
    /// 这里按内容区分：`选项` 后紧跟纯数字/分号 → 分支注释；否则 → 博士候选台词。
    /// </summary>
    private static string NormalizeOptionLine(string mdText)
    {
        const string optionPrefix = "`选项`";
        if (!mdText.StartsWith(optionPrefix))
            return mdText;

        var content = mdText[optionPrefix.Length..];
        if (string.IsNullOrWhiteSpace(content))
            return mdText;

        // 纯数字/分号组成 → 分支编号行（如 `选项`1;2;3），转成注释并强调归属。
        if (OptionIndexRegex().IsMatch(content))
            return $"[可选分支：{content}]（此处为博士/玩家讲话）";

        // 含文本 → 博士候选台词。去掉前后空白，分号分隔的多句保留。
        return $"**博士**（玩家可选台词）：{content.Trim()}";
    }

    private static readonly System.Text.RegularExpressions.Regex _optionIndexRegex =
        new(@"^[0-9;；\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static System.Text.RegularExpressions.Regex OptionIndexRegex() => _optionIndexRegex;

    private static string NormalizeText(ScriptLine entry)
    {
        var mdText = entry.MdText;

        mdText = mdText
            .Replace("`瞳孔地震`", "`震惊`")
            .Replace("`图像平移`", "`场景流转`");

        while (mdText.StartsWith("> "))
            mdText = mdText[2..];

        if (entry.Type == "subtitle")
        {
            mdText = mdText.Replace("`居中字幕`：", "");
            mdText = mdText.Replace("`居中字幕`:", "");
        }

        // Prompt 模式面向 LLM，不携带真实媒体 URL。清空 img/audio/source 的 src 引号内容，
        // 保留标签结构与 alt/class 等语义信息，避免把外部资源链接塞进提示词。
        mdText = MediaSrcRegex().Replace(mdText, "src=\"\"");

        return mdText;
    }

    private static readonly System.Text.RegularExpressions.Regex _mediaSrcRegex =
        new(@"src=\""[^\""]*\""", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static System.Text.RegularExpressions.Regex MediaSrcRegex() => _mediaSrcRegex;

    /// <summary>
    /// 在角色首次说话时输出其 portrait-facts。
    ///
    /// IMPORTANT（Debug 提示）：
    /// 本方法只在"对话行"（同时具备 CharacterName + CharacterCode 的行）上被调用。
    /// 因此"只出场不说话"的角色（立绘行被 RemovePortraitLines 清空、无对话行）不会走到这里，
    /// 其描述由 PortraitProcessor.InjectPortraitFactsForPrompt 在管线早期补注入。
    /// 若发现某角色描述缺失，排查：该角色是否只出场不说话 → 需走 InjectPortraitFactsForPrompt；
    /// 若该角色已说话但无描述 → 检查 CharacterCode 是否为空、PicFacts 是否为空。
    /// </summary>
    private void EmitCharacterFacts(ScriptLine entry, List<string> mdList)
    {
        if (string.IsNullOrEmpty(entry.CharacterName)
            || string.IsNullOrEmpty(entry.CharacterCode)
            || _describedCharacters.Contains(entry.CharacterCode))
            return;

        _describedCharacters.Add(entry.CharacterCode);
        if (!string.IsNullOrEmpty(entry.PicFacts))
        {
            mdList.Add(
                $"<aside class=\"{_config.PortraitFactsClass}\" data-character=\"{AnnotateAsideName(entry)}\">\n{entry.PicFacts}\n</aside>");
        }
    }

    /// <summary>
    /// aside 的 data-character 跨名提示：同一 code 换名时，把全部名字一并列出，便于 LLM 关联立绘与正文。
    /// </summary>
    private string AnnotateAsideName(ScriptLine entry)
    {
        if (string.IsNullOrEmpty(entry.CharacterCode)
            || !_codeToNames.TryGetValue(entry.CharacterCode, out var names)
            || names.Count <= 1)
            return entry.CharacterName;

        return string.Join("（即同一角色）", names);
    }

    private void EmitSceneFacts(ScriptLine entry, List<string> mdList)
    {
        if (!_config.BackgroundTypes.Contains(entry.Type) || string.IsNullOrEmpty(entry.PicFacts))
            return;

        var bgName = "";
        mdList.Add(
            $"<aside class=\"{_config.SceneFactsClass}\" data-bg=\"{bgName}\">\n{entry.PicFacts}\n</aside>");
    }

    private void EmitItemFacts(ScriptLine entry, List<string> mdList)
    {
        if (_config.ItemTypes.Contains(entry.Type) && !string.IsNullOrEmpty(entry.PicFacts))
        {
            mdList.Add($"<aside class=\"{_config.ItemFactsClass}\">\n{entry.PicFacts}\n</aside>");
        }
    }
}