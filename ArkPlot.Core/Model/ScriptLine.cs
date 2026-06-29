using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 游戏剧本中的一行（通用中间表示）。
/// 所有游戏适配器共享此基类，游戏特有字段由子类扩展。
/// </summary>
public class ScriptLine
{
    public int Index { get; set; }

    public string OriginalText { get; set; } = "";

    public string MdText { get; set; } = "";

    public string Type { get; set; } = "";

    public StringDict CommandSet { get; set; } = new();

    public string CharacterName { get; set; } = "";

    public string? CharacterCode { get; set; }

    public string Dialog { get; set; } = "";

    public List<string> ResourceUrls { get; set; } = new();

    public string PicDesc { get; set; } = "";

    [SugarColumn(IsIgnore = true)]
    public string PicFacts { get; set; } = "";

    /// <summary>从 FormattedTextEntry 向上转型的便捷复制。</summary>
    protected void CopyFrom(ScriptLine source)
    {
        Index = source.Index;
        OriginalText = source.OriginalText;
        MdText = source.MdText;
        Type = source.Type;
        CommandSet = new StringDict(source.CommandSet);
        CharacterName = source.CharacterName;
        CharacterCode = source.CharacterCode;
        Dialog = source.Dialog;
        ResourceUrls = new List<string>(source.ResourceUrls);
        PicDesc = source.PicDesc;
        PicFacts = source.PicFacts;
    }
}
