using SqlSugar;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights;

/// <summary>
/// 明日方舟格式化文本条目。
/// 继承 ScriptLine（通用字段已含数据库修饰符），扩展方舟特有字段。
/// </summary>
[SugarTable("FormattedTextEntry")]
public class FormattedTextEntry : ScriptLine
{
    // ──── 方舟特有字段 ────

    public int MdDuplicateCounter { get; set; }

    [SugarColumn(Length = 1000)]
    public string TypText { get; set; } = "";

    public bool IsTagOnly { get; set; }

    public int PngIndex { get; set; }

    [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
    public List<string> Portraits { get; set; } = new();

    /// <summary>立绘焦点：-1=无, 0=单人居中, 1=双人左, 2=三人右</summary>
    public int PortraitFocus { get; set; }

    [SugarColumn(Length = 500)]
    public string Bg { get; set; } = "";

    [SugarColumn(IsIgnore = true)]
    public bool SkipPortraitOutput { get; set; }

    // ──── 构造函数 ────

    public FormattedTextEntry() { }

    public FormattedTextEntry(FormattedTextEntry entry)
    {
        CopyFrom(entry);
        MdDuplicateCounter = entry.MdDuplicateCounter;
        TypText = entry.TypText;
        IsTagOnly = entry.IsTagOnly;
        PngIndex = entry.PngIndex;
        Portraits = new List<string>(entry.Portraits);
        PortraitFocus = entry.PortraitFocus;
        Bg = entry.Bg;
        SkipPortraitOutput = entry.SkipPortraitOutput;
    }

    public bool Validate()
    {
        if (string.IsNullOrEmpty(OriginalText) && string.IsNullOrEmpty(MdText) && string.IsNullOrEmpty(TypText))
            return false;
        if (Index < 0) return false;
        if (MdDuplicateCounter < 0) return false;
        if (PngIndex < 0) return false;
        return true;
    }
}
