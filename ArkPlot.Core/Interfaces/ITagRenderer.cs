using ArkPlot.Core.Model;

namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 标签渲染器：将 ScriptLine 中的游戏特有标签转换为 Markdown 文本。
/// 每种游戏的标签语义和渲染规则不同。
/// </summary>
public interface ITagRenderer
{
    /// <summary>
    /// 将一行 ScriptLine 的 OriginalText 转换为 Markdown，写入 MdText。
    /// </summary>
    /// <param name="line">待渲染的行</param>
    /// <returns>Markdown 文本</returns>
    string RenderLine(ScriptLine line);

    /// <summary>
    /// 渲染器是否需要外部规则文件（如 JSON 标签映射表）。
    /// 如需要，调用方应在首次渲染前提供规则路径。
    /// </summary>
    bool RequiresRulesFile { get; }

    /// <summary>
    /// 加载外部规则文件。仅当 RequiresRulesFile 为 true 时调用。
    /// </summary>
    void LoadRules(string rulesFilePath);
}
