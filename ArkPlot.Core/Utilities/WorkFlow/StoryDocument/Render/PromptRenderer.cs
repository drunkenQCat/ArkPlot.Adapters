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

    public string GroupSeparator => "\r\n\r\n---\r\n\r\n";

    public PromptRenderer(HashSet<string> describedCharacters, PromptRendererConfig? config = null)
    {
        _describedCharacters = describedCharacters;
        _config = config ?? PromptRendererConfig.Arknights;
    }

    public List<string> Render(EntryList grp)
    {
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

            EmitCharacterFacts(entry, mdList);
            mdList.Add(mdText);
            EmitSceneFacts(entry, mdList);
            EmitItemFacts(entry, mdList);
        }
        return mdList;
    }

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
                $"<aside class=\"{_config.PortraitFactsClass}\" data-character=\"{entry.CharacterName}\">\n{entry.PicFacts}\n</aside>");
        }
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