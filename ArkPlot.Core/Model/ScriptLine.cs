using SqlSugar;

namespace ArkPlot.Core.Model;

/// <summary>
/// 游戏剧本中的一行（通用中间表示，可独立持久化）。
/// 所有游戏适配器共享此基类，游戏特有字段由子类扩展。
/// </summary>
[SugarTable("ScriptLine")]
public class ScriptLine
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnDataType = "INTEGER", IsNullable = false)]
    public long PlotId { get; set; }

    [Navigate(NavigateType.ManyToOne, nameof(PlotId))]
    public Plot? Plot { get; set; }

    [SugarColumn(ColumnDataType = "INTEGER")]
    public int Index { get; set; }

    [SugarColumn(Length = 1000)]
    public string OriginalText { get; set; } = "";

    [SugarColumn(Length = 1000)]
    public string MdText { get; set; } = "";

    [SugarColumn(Length = 50)]
    public string Type { get; set; } = "";

    [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
    public StringDict CommandSet { get; set; } = new();

    [SugarColumn(Length = 100)]
    public string CharacterName { get; set; } = "";

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? CharacterCode { get; set; }

    [SugarColumn(Length = 1000)]
    public string Dialog { get; set; } = "";

    [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
    public List<string> ResourceUrls { get; set; } = new();

    [SugarColumn(ColumnDataType = "TEXT")]
    public string PicDesc { get; set; } = "";

    [SugarColumn(IsIgnore = true)]
    public string PicFacts { get; set; } = "";

    /// <summary>从另一个 ScriptLine 复制所有字段。</summary>
    public void CopyFrom(ScriptLine source)
    {
        Id = source.Id;
        PlotId = source.PlotId;
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
