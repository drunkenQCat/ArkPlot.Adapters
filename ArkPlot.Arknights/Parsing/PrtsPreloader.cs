using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

/// <summary>
/// PRTS 剧情预加载器：解析明日方舟标签语法，收集资源。
/// 职责拆分见同目录下的 partial 文件。
/// </summary>
public partial class PrtsPreloader
{
    // ──── 公开状态 ────
    public readonly PreloadSet Assets = [];
    public string PageName => _pageName;
    public string Content = string.Empty;

    // ──── 依赖 ────
    private readonly string _pageName;
    private readonly PrtsDataProcessor _prts = new();
    private readonly List<ScriptLine> _textList;

    // ──── 解析状态 ────
    private int _counter;
    private List<string> _currentPortraits = ["https://pics/transparent.png"];
    private int _currentPortraitFocus;
    private readonly Dictionary<string, string> _slotUrls = new();
    private const string DefaultBg = "https://media.prts.wiki/8/8a/Avg_bg_bg_black.png";
    private string _currentBg = DefaultBg;

    // ──── Override 标志 ────
    private bool _isCharacterNeedsOverride = true;
    private bool _isImageNeedsOverride = true;
    private bool _isTextNeedsOverride = true;
    private bool _isTweenNeedsOverride = true;

    public PrtsPreloader(PlotManager plotManager)
    {
        var pageName = plotManager.CurrentPlot.Title;
        _pageName = pageName
            .Trim()
            .Replace(" 行动后", "/END")
            .Replace(" 行动前", "/BEG")
            .Replace(" 幕间", "/NBT");
        _textList = plotManager.CurrentPlot.TextVariants;
        Content = plotManager.CurrentPlot.Content.ToString();
    }

    public void ParseAndCollectAssets()
    {
        foreach (var entry in _textList.OfType<FormattedTextEntry>())
            ParseOriginalText(entry);
    }

    /// <summary>解析一行原始文本，填充 FormattedTextEntry 的各字段。</summary>
    public void ParseOriginalText(FormattedTextEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.OriginalText)
            || entry.OriginalText.TrimStart().StartsWith("//"))
            return;

        OverrideCurrentText();

        var match = ArkPlotRegs.UniversalTagsRegex().Match(entry.OriginalText);
        if (!match.Success)
        {
            entry.Dialog = entry.OriginalText;
            ApplyCurrentState(entry);
            return;
        }

        var matched = ExtractMatch(match);
        SetCharacterNameFromMatch(entry, matched);
        entry.Dialog = match.Groups[5].Value;

        if (string.IsNullOrEmpty(matched.Tag))
        {
            HandleTaglessLine(entry, matched);
            return;
        }

        HandleTaggedLine(entry, matched);
    }

    private static Matched ExtractMatch(System.Text.RegularExpressions.Match match)
    {
        return new Matched
        {
            Tag = match.Groups[1].Value,
            Commands = match.Groups[2].Value,
            TagOnly = match.Groups[3].Value,
            CharName = match.Groups[4].Value,
        };
    }

    private static void SetCharacterNameFromMatch(FormattedTextEntry entry, Matched matched)
    {
        if (!string.IsNullOrEmpty(matched.CharName) && matched.CharName.StartsWith("name="))
            entry.CharacterName = matched.CharName.Split('"')[1];
        entry.IsTagOnly = !string.IsNullOrEmpty(matched.TagOnly);
    }

    private void HandleTaglessLine(FormattedTextEntry entry, Matched matched)
    {
        if (!string.IsNullOrEmpty(matched.TagOnly))
        {
            var tagOnly = matched.TagOnly.Trim().ToLowerInvariant();
            if (tagOnly is "charslot" or "character")
            {
                entry.CommandSet = ProcessCommand(tagOnly, "", out var urlList);
                entry.Type = entry.CommandSet["type"];
                entry.ResourceUrls = urlList;
                entry.CharacterCode = null;
            }
        }
        ApplyCurrentState(entry);
    }

    private void HandleTaggedLine(FormattedTextEntry entry, Matched matched)
    {
        entry.CommandSet = ProcessCommand(matched.Tag.ToLower(), matched.Commands, out var urlList);
        entry.Type = entry.CommandSet["type"];

        if (entry.Type == "sticker")
            entry.Dialog = GetStickerText(entry);

        if (entry.Type == "subtitle" && entry.CommandSet.TryGetValue("text", out var subtitle))
            entry.Dialog = subtitle ?? "";

        entry.ResourceUrls = urlList;
        ResolveSkipPortrait(entry);
        ResolveCharacterCode(entry);

        if (entry.Type == "multiline")
        {
            entry.CommandSet.TryGetValue("name", out var charName);
            entry.CharacterName = charName ?? "";
        }

        ApplyCurrentState(entry);
    }

    private static void ResolveSkipPortrait(FormattedTextEntry entry)
    {
        if (entry.Type == "charslot"
            && entry.ResourceUrls.Count == 0
            && entry.CommandSet.TryGetValue("focus", out var fn)
            && fn == "none")
        {
            entry.SkipPortraitOutput = true;
        }
    }

    private void ResolveCharacterCode(FormattedTextEntry entry)
    {
        var isPortraitType = entry.Type is "character" or "charactercutin" or "charslot";
        if (!isPortraitType)
        {
            entry.CharacterCode = null;
            return;
        }

        var focusName = "name";
        if (entry.CommandSet.TryGetValue("focus", out var focusVal)
            && focusVal == "2"
            && entry.CommandSet.ContainsKey("name2"))
        {
            focusName = "name2";
        }

        if (entry.CommandSet.TryGetValue(focusName, out var rawName))
            entry.CharacterCode = _prts.GetCharacterCode(rawName.ToLower());
    }

    /// <summary>将当前立绘/背景状态写入 entry。</summary>
    private void ApplyCurrentState(FormattedTextEntry entry)
    {
        entry.Portraits = _currentPortraits;
        entry.PortraitFocus = _currentPortraitFocus;
        entry.Bg = _currentBg;
        _counter++;
    }

    private static string GetStickerText(FormattedTextEntry line)
    {
        if (!line.CommandSet.TryGetValue("text", out var text) || string.IsNullOrEmpty(text))
            return "";
        var clean = text.Replace(@"\n", "");
        if (!clean.StartsWith('<'))
            return clean;
        return RoundedWithTagRegex().Replace(clean, "");
    }

    [System.Text.RegularExpressions.GeneratedRegex("<.*?>")]
    private static partial System.Text.RegularExpressions.Regex RoundedWithTagRegex();
}
