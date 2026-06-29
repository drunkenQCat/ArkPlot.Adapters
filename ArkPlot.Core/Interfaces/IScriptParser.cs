using ArkPlot.Core.Model;

namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 脚本解析器：将游戏原始脚本文本解析为通用 ScriptLine 序列。
/// 每种游戏实现一个解析器（如 ArkNights 解析 [name=][charslot][background] 等标签语法）。
/// </summary>
public interface IScriptParser
{
    /// <summary>游戏唯一标识，如 "arknights"。</summary>
    string GameId { get; }

    /// <summary>
    /// 将原始脚本文本解析为 ScriptLine 列表。
    /// 实现应处理：类型识别、命令参数提取、角色名/代码填充、资源 URL 收集。
    /// 解析是有状态的（当前背景、当前立绘等累积状态按行顺序传播）。
    /// </summary>
    /// <param name="rawText">章节原始文本（由 IStoryDataProvider 下载）</param>
    /// <param name="chapterTitle">章节标题（用于日志/调试）</param>
    /// <returns>解析后的 ScriptLine 列表，按行号排序</returns>
    List<ScriptLine> Parse(string rawText, string chapterTitle);

    /// <summary>
    /// 收集该章节引用的所有外部资源（图片/音频 URL），用于预下载。
    /// </summary>
    /// <param name="lines">已解析的 ScriptLine 列表</param>
    /// <returns>去重的 (资源类型, URL) 集合</returns>
    HashSet<ResourceRef> CollectResources(List<ScriptLine> lines);
}

/// <summary>
/// 脚本引用的外部资源。
/// </summary>
public record ResourceRef(string ResourceType, string Url);
